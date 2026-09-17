package com.fromthefarm.app.ui

import android.content.Context
import com.fromthefarm.app.data.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.*
import org.junit.After
import org.junit.Assert.*
import org.junit.Before
import org.junit.Test
import retrofit2.HttpException
import retrofit2.Response
import okhttp3.ResponseBody.Companion.toResponseBody

@OptIn(ExperimentalCoroutinesApi::class)
class FarmViewModelTest {
    private val dispatcher = StandardTestDispatcher()
    @Before fun setup() { Dispatchers.setMain(dispatcher) }
    @After fun cleanup() { Dispatchers.resetMain() }

    @Test fun savedFirebaseSessionDoesNotUnlockAppUntilApiAcceptsIt() = runTest(dispatcher) {
        val source = FakeSource()
        val vm = FarmViewModel(source)
        assertNull(vm.state.value.profile)
        assertTrue(vm.state.value.busy)
        advanceUntilIdle()
        assertEquals("user-one", vm.state.value.profile?.userId)
        assertEquals(1, source.backend.sessions)
    }

    @Test fun rejectedSessionClearsAllPrivateStateAndSignsOut() = runTest(dispatcher) {
        val source = FakeSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()
        vm.refresh(); advanceUntilIdle()
        assertEquals(1, vm.state.value.listings.size)
        source.backend.reject = true
        vm.refresh(); advanceUntilIdle()
        assertNull(vm.state.value.profile)
        assertTrue(vm.state.value.listings.isEmpty())
        assertTrue(source.loggedOut)
        assertNotNull(vm.state.value.error)
    }

    @Test fun matchFailureStillLoadsListingsAndDemand() = runTest(dispatcher) {
        val source = FakeSource()
        source.backend.matchFailure = true
        val vm = FarmViewModel(source)
        advanceUntilIdle()
        vm.refresh(); advanceUntilIdle()
        assertEquals(1, vm.state.value.listings.size)
        assertEquals(1, vm.state.value.demands.size)
        assertTrue(vm.state.value.error!!.contains("matches"))
        assertTrue(vm.state.value.loaded)
    }

    @Test fun repeatedSaveTapsMakeOneRequestAndCloseOnlyAfterSuccess() = runTest(dispatcher) {
        val source = FakeSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()
        var closed = 0
        val body = ListingWrite("Tomatoes", 12.5, "kg", "2026-09-16", Location(-29.85, 31.02))
        vm.saveListing(null, body) { closed++ }
        vm.saveListing(null, body) { closed++ }
        assertEquals(0, closed)
        advanceUntilIdle()
        assertEquals(1, source.backend.writes)
        assertEquals(1, closed)
        source.backend.writeFailure = true
        vm.saveListing(null, body) { closed++ }
        advanceUntilIdle()
        assertEquals(1, closed)
        assertNotNull(vm.state.value.error)
    }

    @Test fun logoutClearsDataEvenIfProviderCleanupFails() = runTest(dispatcher) {
        val source = FakeSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()
        vm.refresh(); advanceUntilIdle()
        source.cleanupFailure = true
        vm.logout(); advanceUntilIdle()
        assertNull(vm.state.value.profile)
        assertTrue(vm.state.value.matches.isEmpty())
        assertTrue(vm.state.value.listings.isEmpty())
    }

    @Test fun openingAnotherMatchImmediatelyClearsPreviousContactDetails() = runTest(dispatcher) {
        val source = FakeSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()
        source.backend.matchStatus = "Confirmed"
        vm.openMatch("first"); advanceUntilIdle()
        assertNotNull(vm.state.value.detail)
        vm.openMatch("second")
        assertNull(vm.state.value.detail)
        advanceUntilIdle()
        assertEquals("second", vm.state.value.detail?.matchId)
    }

    @Test fun ratingsRequireCompletedMatchAndCannotRepeatInSession() = runTest(dispatcher) {
        val source = FakeSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()
        vm.openMatch("m1"); advanceUntilIdle()
        vm.rate("m1", true); advanceUntilIdle()
        assertEquals(0, source.backend.ratings)
        source.backend.matchStatus = "Completed"
        vm.openMatch("m1"); advanceUntilIdle()
        vm.rate("m1", true); advanceUntilIdle()
        vm.rate("m1", false); advanceUntilIdle()
        assertEquals(1, source.backend.ratings)
        assertTrue("m1" in vm.state.value.ratedMatches)
    }

    @Test fun completionRequiresConfirmationAndUnlocksRating() = runTest(dispatcher) {
        val source = FakeSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()
        vm.openMatch("m1"); advanceUntilIdle()
        vm.complete("m1"); advanceUntilIdle()
        assertEquals(0, source.backend.completions)
        source.backend.matchStatus = "Confirmed"
        vm.openMatch("m1"); advanceUntilIdle()
        vm.complete("other"); advanceUntilIdle()
        assertEquals(0, source.backend.completions)
        vm.complete("m1"); advanceUntilIdle()
        assertEquals(1, source.backend.completions)
        assertEquals("Completed", vm.state.value.detail?.status)
        vm.complete("m1"); advanceUntilIdle()
        assertEquals(1, source.backend.completions)
        vm.rate("m1", true); advanceUntilIdle()
        assertEquals(1, source.backend.ratings)
    }

    @Test fun unavailableBackendRetainsFirebaseSessionWithoutUnlockingFarmScreens() = runTest(dispatcher) {
        val source = FakeSource()
        source.backend.sessionFailure = true
        val vm = FarmViewModel(source)
        advanceUntilIdle()
        assertTrue(vm.state.value.firebaseSignedIn)
        assertNull(vm.state.value.profile)
        assertNotNull(vm.state.value.error)
        assertFalse(source.loggedOut)
        source.backend.sessionFailure = false
        vm.resumeSession(); advanceUntilIdle()
        assertNotNull(vm.state.value.profile)
    }

    @Test fun biometricSessionStartsLockedAndCannotCallBackendBeforeUnlock() = runTest(dispatcher) {
        val source = FakeSource().apply { deviceBiometrics = true }
        val vm = FarmViewModel(source)
        advanceUntilIdle()
        assertTrue(vm.state.value.biometricLocked)
        vm.resumeSession(); vm.refresh(); advanceUntilIdle()
        assertEquals(0, source.backend.sessions)
        vm.biometricError("Cancelled")
        assertTrue(vm.state.value.biometricLocked)
        vm.disableBiometrics()
        assertTrue(source.deviceBiometrics)
        vm.unlockWithBiometrics(); advanceUntilIdle()
        assertFalse(vm.state.value.biometricLocked)
        assertEquals(1, source.backend.sessions)
        vm.lock()
        assertTrue(vm.state.value.biometricLocked)
        vm.logout(); advanceUntilIdle()
        assertFalse(vm.state.value.biometricLocked)
        assertNull(vm.state.value.profile)
    }

    @Test fun biometricUnlockStillRequiresBackendForProfile() = runTest(dispatcher) {
        val source = FakeSource().apply { deviceBiometrics = true; backend.sessionFailure = true }
        val vm = FarmViewModel(source)
        vm.unlockWithBiometrics(); advanceUntilIdle()
        assertFalse(vm.state.value.biometricLocked)
        assertTrue(vm.state.value.firebaseSignedIn)
        assertNull(vm.state.value.profile)
        assertNotNull(vm.state.value.error)
    }

    private class FakeSource : FarmDataSource {
        val backend = FakeApi()
        override val api: FarmApi = backend
        var loggedOut = false
        var cleanupFailure = false
        var deviceBiometrics = false
        override fun biometricEnabled() = deviceBiometrics
        override fun setBiometricEnabled(enabled: Boolean) { deviceBiometrics = enabled }
        override fun hasSession() = true
        override suspend fun signIn(activity: Context) = Unit
        override suspend fun token() = "Bearer test"
        override suspend fun logout() { loggedOut = true; if (cleanupFailure) error("cleanup failed") }
    }

    private class FakeApi : FarmApi {
        var sessions = 0
        var writes = 0
        var reject = false
        var matchFailure = false
        var writeFailure = false
        var matchStatus = "Suggested"
        var ratings = 0
        var completions = 0
        var sessionFailure = false
        val user = Profile("user-one", "Farmer", "Farmer", "en", 10, true, false, null)
        val listing = Listing("l1", "Tomatoes", 12.5, "kg", "2026-09-16", Location(-29.85, 31.02), "Active")
        val demand = Demand("d1", "Tomatoes", 10.0, "kg", "2026-09-17", listing.location, "Open")
        private fun authorize() { if (reject) throw HttpException(Response.error<Unit>(401, "".toResponseBody())) }
        override suspend fun session(token: String, body: SessionRequest): Session {
            authorize()
            if (sessionFailure) throw java.io.IOException("Service unavailable")
            sessions++
            return Session("user-one", true)
        }
        override suspend fun profile(token: String): Profile { authorize(); return user }
        override suspend fun profile(token: String, body: ProfileUpdate) = user.copy(role = body.role)
        override suspend fun listings(token: String, mine: Boolean): List<Listing> { authorize(); return listOf(listing) }
        override suspend fun browseListings(token: String, crop: String?, radius: Int, latitude: Double, longitude: Double) = listOf(listing)
        override suspend fun demands(token: String, mine: Boolean) = listOf(demand)
        override suspend fun matches(token: String): List<Match> { if (matchFailure) error("unavailable"); return emptyList() }
        override suspend fun createListing(token: String, body: ListingWrite): Listing { writes++; if (writeFailure) error("write failed"); return listing }
        override suspend fun updateListing(token: String, id: String, body: ListingWrite) = listing
        override suspend fun deleteListing(token: String, id: String) = Unit
        override suspend fun createDemand(token: String, body: DemandWrite) = demand
        override suspend fun updateDemand(token: String, id: String, body: DemandWrite) = demand
        override suspend fun deleteDemand(token: String, id: String) = Unit
        override suspend fun detail(token: String, id: String) = MatchDetail(id, .8, matchStatus, Contact(null, null))
        override suspend fun confirm(token: String, id: String) = Unit
        override suspend fun complete(token: String, id: String) { completions++; matchStatus = "Completed" }
        override suspend fun rate(token: String, id: String, body: RatingWrite) { ratings++ }
    }
}
