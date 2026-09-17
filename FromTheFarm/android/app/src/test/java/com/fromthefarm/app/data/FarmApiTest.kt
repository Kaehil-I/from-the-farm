package com.fromthefarm.app.data

import kotlinx.coroutines.runBlocking
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.*
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

class FarmApiTest {
    private val server = MockWebServer()
    private val api = Retrofit.Builder().baseUrl(server.url("/api/v1/"))
        .addConverterFactory(GsonConverterFactory.create()).build().create(FarmApi::class.java)
    @After fun close() { server.shutdown() }
    @Test fun sessionUsesFirebaseBearerHeaderAndDeviceLanguage() = runBlocking {
        server.enqueue(MockResponse().setBody("""{"userId":"firebase-uid","onboardingComplete":false}"""))
        assertFalse(api.session("Bearer test-token", SessionRequest("zu")).onboardingComplete)
        val request = server.takeRequest()
        assertEquals("/api/v1/auth/session", request.path)
        assertEquals("Bearer test-token", request.getHeader("Authorization"))
        assertEquals("""{"deviceLanguage":"zu"}""", request.body.readUtf8())
    }
    @Test fun readsActualControllerListingSchema() = runBlocking {
        server.enqueue(MockResponse().setBody("""[{"id":"l1","cropType":"Tomatoes","quantity":12.5,"unit":"kg","harvestDate":"2026-09-15","location":{"latitude":-29.85,"longitude":31.02},"status":"Active"}]"""))
        val result = api.listings("Bearer test", true).single()
        assertEquals("l1", result.id)
        assertEquals(12.5, result.quantity, 0.0)
        assertEquals("/api/v1/listings?mine=true", server.takeRequest().path)
    }
    @Test fun handlesNoContentConfirmationAndDeterministicMatchId() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(204))
        api.confirm("Bearer test", "listing:demand")
        val request = server.takeRequest()
        assertEquals("POST", request.method)
        assertEquals("/api/v1/matches/listing:demand/confirm", request.path)
    }
    @Test fun completionUsesAuthenticatedPostAndAcceptsNoContent() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(204))
        api.complete("Bearer test", "listing:demand")
        val request = server.takeRequest()
        assertEquals("POST", request.method)
        assertEquals("/api/v1/matches/listing:demand/complete", request.path)
        assertEquals("Bearer test", request.getHeader("Authorization"))
    }
    @Test fun suggestedMatchCanHaveNoContactObject() = runBlocking {
        server.enqueue(MockResponse().setBody("""{"matchId":"m1","score":0.8,"status":"Suggested","counterpartContact":null}"""))
        val detail = api.detail("Bearer test", "m1")
        assertNull(detail.counterpartContact)
        assertEquals("Suggested", detail.status)
    }
    @Test fun listingPhotoIsIncludedInCreateRequest() = runBlocking {
        server.enqueue(MockResponse().setBody("""{"id":"l1","cropType":"Tomatoes","quantity":1.0,"unit":"kg","harvestDate":"2026-09-16","location":{"latitude":-29.0,"longitude":31.0},"status":"Active","photoUrl":"data:image/jpeg;base64,test"}"""))
        val listing = api.createListing("Bearer test", ListingWrite("Tomatoes", 1.0, "kg", "2026-09-16", Location(-29.0, 31.0), "test"))
        assertEquals("data:image/jpeg;base64,test", listing.photoUrl)
        val body = com.google.gson.JsonParser.parseString(server.takeRequest().body.readUtf8()).asJsonObject
        assertEquals("test", body.get("photoBase64").asString)
    }
    @Test fun distanceSearchSendsCoordinatesAndRadiusToServer() = runBlocking {
        server.enqueue(MockResponse().setBody("[]"))
        api.browseListings("Bearer test", "Tomatoes", 20, -29.85, 31.02)
        val url = server.takeRequest().requestUrl!!
        assertEquals("Tomatoes", url.queryParameter("cropType"))
        assertEquals("20", url.queryParameter("maxDistanceKm"))
        assertEquals("-29.85", url.queryParameter("latitude"))
        assertEquals("31.02", url.queryParameter("longitude"))
    }

    @Test fun demandWriteUsesQuantityNeededAndIsoDate() = runBlocking {
        server.enqueue(MockResponse().setBody("""{"id":"d1","cropType":"Tomatoes","quantityNeeded":12.5,"unit":"kg","deadline":"2026-09-16","location":{"latitude":-29.85,"longitude":31.02},"status":"Open"}"""))
        val result = api.createDemand("Bearer test", DemandWrite("Tomatoes", 12.5, "kg", "2026-09-16", Location(-29.85, 31.02)))
        assertEquals("d1", result.id)
        val request = server.takeRequest()
        assertEquals("POST", request.method)
        assertEquals("/api/v1/demands", request.path)
        val body = com.google.gson.JsonParser.parseString(request.body.readUtf8()).asJsonObject
        assertEquals(12.5, body["quantityNeeded"].asDouble, 0.0)
        assertEquals("2026-09-16", body["deadline"].asString)
        assertFalse(body.has("quantity"))
    }

    @Test fun usesSingularRatingEndpoint() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(201))
        api.rate("Bearer test", "m1", RatingWrite(true))
        val request = server.takeRequest()
        assertEquals("/api/v1/matches/m1/rating", request.path)
        assertEquals("""{"thumbsUp":true}""", request.body.readUtf8())
    }
}
