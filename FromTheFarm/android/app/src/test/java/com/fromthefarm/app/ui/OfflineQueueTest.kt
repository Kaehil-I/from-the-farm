package com.fromthefarm.app.ui

import android.content.Context
import com.fromthefarm.app.data.*
import com.fromthefarm.app.data.offline.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.After
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import retrofit2.HttpException
import retrofit2.Response
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class OfflineQueueTest {
    private val dispatcher = StandardTestDispatcher()
    @Before fun setup() { Dispatchers.setMain(dispatcher) }
    @After fun cleanup() { Dispatchers.resetMain() }

    private val api = QueueApi()
    private val connectivity = MutableStateFlow(true)
    private val scheduler = CountingScheduler()
    private val body = ListingWrite("Tomatoes", 12.5, "kg", "2026-09-16", Location(-29.85, 31.02))

    private fun viewModel() = FarmViewModel(Source(api), OfflineSupport(InMemoryOfflineStore(), scheduler, connectivity))
    private fun goOffline() { api.online = false; connectivity.value = false }
    private fun goOnline() { api.online = true; connectivity.value = true }

    @Test fun createWithoutAConnectionIsQueuedAndTheFormCloses() = runTest(dispatcher) {
        val vm = viewModel(); advanceUntilIdle()
        goOffline(); advanceUntilIdle()
        var closed = 0

        vm.saveListing(null, body) { closed++ }; advanceUntilIdle()

        assertEquals(1, closed)
        assertNull(vm.state.value.error)
        assertTrue(vm.state.value.message!!.contains("Saved on this phone"))
        assertEquals(1, vm.sync.value.pendingCount)
        assertTrue(vm.sync.value.offline)
        assertEquals(1, scheduler.scheduled)
        assertTrue(api.stored.isEmpty())
    }

    @Test fun reconnectingUploadsTheQueueAndAsksForARefresh() = runTest(dispatcher) {
        val vm = viewModel(); advanceUntilIdle()
        goOffline(); advanceUntilIdle()
        vm.saveListing(null, body) { }; advanceUntilIdle()

        goOnline(); advanceUntilIdle()

        assertEquals(1, api.stored.size)
        assertEquals(0, vm.sync.value.pendingCount)
        assertFalse(vm.sync.value.offline)
        assertTrue(vm.state.value.message!!.contains("Uploaded 1 record"))
        assertFalse("the lists reload to show the new record", vm.state.value.loaded)
    }

    @Test fun aLostResponseDoesNotCreateTheRecordTwice() = runTest(dispatcher) {
        val vm = viewModel(); advanceUntilIdle()
        api.loseNextResponse = true   // the server saves the record, but the phone never hears back
        vm.saveListing(null, body) { }; advanceUntilIdle()
        assertEquals("the server already has it", 1, api.stored.size)
        assertEquals("and the phone queued it to be safe", 1, vm.sync.value.pendingCount)

        goOnline(); vm.syncNow(); advanceUntilIdle()

        assertEquals("the retry reused the same id, so there is still one record", 1, api.stored.size)
        assertEquals(2, api.requests)
        assertEquals(0, vm.sync.value.pendingCount)
    }

    @Test fun aRefusedCreateIsShownAsAnErrorNotQueued() = runTest(dispatcher) {
        val vm = viewModel(); advanceUntilIdle()
        api.rejectWith = 400
        var closed = 0

        vm.saveListing(null, body) { closed++ }; advanceUntilIdle()

        assertEquals(0, closed)
        assertNotNull(vm.state.value.error)
        assertEquals(0, vm.sync.value.records.size)
    }

    @Test fun demandsAreQueuedTheSameWay() = runTest(dispatcher) {
        val vm = viewModel(); advanceUntilIdle()
        goOffline(); advanceUntilIdle()
        var closed = 0

        vm.saveDemand(null, DemandWrite("Tomatoes", 10.0, "kg", "2026-09-30", Location(-29.85, 31.02))) { closed++ }
        advanceUntilIdle()

        assertEquals(1, closed)
        assertEquals(RecordKind.DEMAND, vm.sync.value.records.single().kind)
        goOnline(); advanceUntilIdle()
        assertEquals(1, api.storedDemands.size)
        assertEquals(0, vm.sync.value.pendingCount)
    }

    @Test fun aFailedRecordCanBeRetriedOrDiscarded() = runTest(dispatcher) {
        val vm = viewModel(); advanceUntilIdle()
        goOffline(); advanceUntilIdle()
        vm.saveListing(null, body) { }; advanceUntilIdle()
        api.rejectWith = 400; goOnline(); advanceUntilIdle()
        val id = vm.sync.value.records.single().clientGeneratedId
        assertEquals(1, vm.sync.value.failedCount)
        assertEquals(SyncStatus.FAILED, vm.sync.value.records.single().status)

        api.rejectWith = null
        vm.retryRecord(id); advanceUntilIdle()
        assertEquals(0, vm.sync.value.records.size)
        assertEquals(1, api.stored.size)

        goOffline(); advanceUntilIdle()
        vm.saveListing(null, body) { }; advanceUntilIdle()
        vm.discardRecord(vm.sync.value.records.single().clientGeneratedId); advanceUntilIdle()
        assertEquals(0, vm.sync.value.records.size)
    }

    @Test fun signingOutStopsShowingTheQueueAndClearsSavedData() = runTest(dispatcher) {
        val vm = viewModel(); advanceUntilIdle()
        vm.refresh(); advanceUntilIdle()
        goOffline(); advanceUntilIdle()
        vm.saveListing(null, body) { }; advanceUntilIdle()
        assertEquals(1, vm.sync.value.records.size)

        vm.logout(); advanceUntilIdle()

        assertEquals(0, vm.sync.value.records.size)
        assertNull(vm.state.value.profile)
    }

    private class CountingScheduler : SyncScheduler { var scheduled = 0; override fun schedule() { scheduled++ } }

    private class Source(override val api: FarmApi) : FarmDataSource {
        override fun hasSession() = true
        override suspend fun signIn(activity: Context) = Unit
        override suspend fun token() = "Bearer test"
        override fun currentUserId() = "u1"
        override suspend fun logout() = Unit
    }

    /** Stands in for the server. Like the real one, it keeps one record per clientGeneratedId. */
    private class QueueApi : FarmApi {
        var online = true
        var loseNextResponse = false
        var rejectWith: Int? = null
        var requests = 0
        val stored = linkedMapOf<String, ListingWrite>()
        val storedDemands = linkedMapOf<String, DemandWrite>()
        private val me = Profile("u1", "Farmer", "Farmer", "en", 10, true, false, null)
        private val where = Location(-29.85, 31.02)

        private fun gate() {
            requests++
            if (!online) throw IOException("offline")
            rejectWith?.let { throw HttpException(Response.error<Unit>(it, "".toResponseBody())) }
        }
        override suspend fun createListing(token: String, body: ListingWrite): Listing {
            gate()
            stored.getOrPut(body.clientGeneratedId ?: "none-${stored.size}") { body }
            if (loseNextResponse) { loseNextResponse = false; online = false; throw IOException("connection lost") }
            return Listing("l${stored.size}", body.cropType, body.quantity, body.unit, body.harvestDate, body.location, "Active")
        }
        override suspend fun createDemand(token: String, body: DemandWrite): Demand {
            gate()
            storedDemands.getOrPut(body.clientGeneratedId ?: "none-${storedDemands.size}") { body }
            return Demand("d${storedDemands.size}", body.cropType, body.quantityNeeded, body.unit, body.deadline, body.location, "Open")
        }
        override suspend fun session(token: String, body: SessionRequest) = Session("u1", true)
        override suspend fun profile(token: String) = me
        override suspend fun profile(token: String, body: ProfileUpdate) = me
        override suspend fun listings(token: String, mine: Boolean): List<Listing> {
            if (!online) throw IOException("offline")
            return stored.values.mapIndexed { i, w -> Listing("l${i + 1}", w.cropType, w.quantity, w.unit, w.harvestDate, where, "Active") }
        }
        override suspend fun demands(token: String, mine: Boolean): List<Demand> {
            if (!online) throw IOException("offline")
            return emptyList()
        }
        override suspend fun matches(token: String): List<Match> {
            if (!online) throw IOException("offline")
            return emptyList()
        }
        override suspend fun browseListings(token: String, crop: String?, radius: Int, latitude: Double, longitude: Double) = emptyList<Listing>()
        override suspend fun updateListing(token: String, id: String, body: ListingWrite) = TODO()
        override suspend fun deleteListing(token: String, id: String) = Unit
        override suspend fun updateDemand(token: String, id: String, body: DemandWrite) = TODO()
        override suspend fun deleteDemand(token: String, id: String) = Unit
        override suspend fun detail(token: String, id: String) = TODO()
        override suspend fun confirm(token: String, id: String) = Unit
        override suspend fun complete(token: String, id: String) = Unit
        override suspend fun rate(token: String, id: String, body: RatingWrite) = Unit
        override suspend fun registerDevice(token: String, body: DeviceTokenWrite) = Unit
        override suspend fun unregisterDevice(token: String, body: DeviceTokenWrite) = Unit
    }
}
