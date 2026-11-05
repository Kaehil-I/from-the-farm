package com.fromthefarm.app.data.offline

import com.fromthefarm.app.data.FarmApi
import com.fromthefarm.app.data.ListingWrite
import com.fromthefarm.app.data.Location
import com.google.gson.Gson
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.SocketPolicy
import org.junit.After
import org.junit.Assert.*
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

class OutboxProcessorTest {
    private val server = MockWebServer()
    // Same retry setting as the real client: a dropped connection surfaces as an error instead of a silent resend.
    private val client = OkHttpClient.Builder().retryOnConnectionFailure(false)
        .connectTimeout(2, TimeUnit.SECONDS).readTimeout(2, TimeUnit.SECONDS).build()
    private val api = Retrofit.Builder().baseUrl(server.url("/api/v1/")).client(client)
        .addConverterFactory(GsonConverterFactory.create()).build().create(FarmApi::class.java)
    private val store = InMemoryOfflineStore()
    private val gson = Gson()
    private val created = """{"id":"l1","cropType":"Tomatoes","quantity":12.5,"unit":"kg","harvestDate":"2026-09-16","location":{"latitude":-29.85,"longitude":31.02},"status":"Active"}"""
    private val token: suspend () -> String = { "Bearer test" }

    @After fun close() { server.shutdown() }

    private suspend fun queue(clientId: String, createdAt: Long, owner: String = "u1") {
        val write = ListingWrite("Tomatoes", 12.5, "kg", "2026-09-16", Location(-29.85, 31.02), clientGeneratedId = clientId)
        store.enqueue(OutboxEntry(ownerId = owner, clientGeneratedId = clientId, kind = RecordKind.LISTING.name,
            payload = gson.toJson(write), title = "Tomatoes", detail = "12.5 kg", createdAt = createdAt))
    }

    @Test fun uploadsOldestFirstAndEmptiesTheQueue() = runBlocking {
        queue("second", createdAt = 2); queue("first", createdAt = 1)
        server.enqueue(MockResponse().setBody(created)); server.enqueue(MockResponse().setBody(created))

        val result = OutboxProcessor(store, api).drain("u1", token)

        assertEquals(DrainResult(2, DrainOutcome.DONE), result)
        assertTrue(server.takeRequest().body.readUtf8().contains("\"clientGeneratedId\":\"first\""))
        assertTrue(server.takeRequest().body.readUtf8().contains("\"clientGeneratedId\":\"second\""))
        assertTrue(store.pending("u1").isEmpty())
    }

    @Test fun droppedConnectionKeepsTheRecordAndTheRetryReusesItsId() = runBlocking {
        queue("abc", createdAt = 1)
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.DISCONNECT_AT_START))

        val first = OutboxProcessor(store, api).drain("u1", token)
        assertEquals(DrainResult(0, DrainOutcome.RETRY_LATER), first)
        assertEquals(1, store.pending("u1").size)
        assertEquals("a dropped connection is not the record's fault", 0, store.pending("u1").single().attempts)

        server.enqueue(MockResponse().setBody(created))
        val second = OutboxProcessor(store, api).drain("u1", token)
        assertEquals(DrainResult(1, DrainOutcome.DONE), second)
        // The server may or may not log the dropped attempt; the last request it saw is the retry.
        repeat(server.requestCount - 1) { server.takeRequest() }
        assertTrue(server.takeRequest().body.readUtf8().contains("\"clientGeneratedId\":\"abc\""))
        assertTrue(store.pending("u1").isEmpty())
    }

    @Test fun serverRefusalMarksOneRecordFailedAndTheNextStillUploads() = runBlocking {
        queue("bad", createdAt = 1); queue("good", createdAt = 2)
        server.enqueue(MockResponse().setResponseCode(400)); server.enqueue(MockResponse().setBody(created))

        val result = OutboxProcessor(store, api).drain("u1", token)

        assertEquals(DrainResult(1, DrainOutcome.DONE), result)
        val failed = store.find("u1", "bad")!!
        assertEquals(SyncStatus.FAILED.name, failed.status)
        assertTrue(failed.lastError!!.contains("400"))
        assertNull(store.find("u1", "good"))
    }

    @Test fun serviceErrorsAreRetriedThenGivenUp() = runBlocking {
        queue("flaky", createdAt = 1)
        val processor = OutboxProcessor(store, api, maxAttempts = 2)
        server.enqueue(MockResponse().setResponseCode(503)); server.enqueue(MockResponse().setResponseCode(503))

        assertEquals(DrainOutcome.RETRY_LATER, processor.drain("u1", token).outcome)
        assertEquals(SyncStatus.PENDING.name, store.find("u1", "flaky")!!.status)
        assertEquals(DrainOutcome.DONE, processor.drain("u1", token).outcome)
        val entry = store.find("u1", "flaky")!!
        assertEquals(SyncStatus.FAILED.name, entry.status)
        assertEquals(2, entry.attempts)
    }

    @Test fun rejectedLoginLeavesEverythingQueued() = runBlocking {
        queue("keep", createdAt = 1)
        server.enqueue(MockResponse().setResponseCode(401))

        assertEquals(DrainOutcome.NEEDS_SIGN_IN, OutboxProcessor(store, api).drain("u1", token).outcome)
        assertEquals(SyncStatus.PENDING.name, store.find("u1", "keep")!!.status)
    }

    @Test fun onlyTheSignedInAccountsRecordsAreSent() = runBlocking {
        queue("mine", createdAt = 1, owner = "u1"); queue("theirs", createdAt = 2, owner = "u2")
        server.enqueue(MockResponse().setBody(created))

        assertEquals(1, OutboxProcessor(store, api).drain("u1", token).uploaded)
        assertEquals(1, server.requestCount)
        assertNotNull(store.find("u2", "theirs"))
    }

    @Test fun queueingTheSameIdTwiceKeepsOneEntry() = runBlocking {
        queue("same", createdAt = 1); queue("same", createdAt = 2)
        assertEquals(1, store.pending("u1").size)
    }
}
