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

/**
 * Settings tab behaviour. The screen edits the signed-in profile through
 * PUT /users/me, so these assert on what actually reaches the API rather than
 * on what the form happened to be rendered with.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class SettingsProfileTest {
    // StandardTestDispatcher queues coroutines instead of running them eagerly,
    // so nothing the view model launches executes until advanceUntilIdle() is
    // called. That makes each assertion run at a known point rather than racing
    // the work it is checking.
    // Reference: https://kotlinlang.org/api/kotlinx.coroutines/kotlinx-coroutines-test/kotlinx.coroutines.test/-standard-test-dispatcher.html
    private val dispatcher = StandardTestDispatcher()

    // The view model launches on Dispatchers.Main, which has no implementation
    // in a JVM unit test, so it is swapped for the test dispatcher and reset
    // afterwards to keep the tests independent of each other.
    @Before fun setup() { Dispatchers.setMain(dispatcher) }
    @After fun cleanup() { Dispatchers.resetMain() }

    @Test fun savingPreferencesSendsTheEditedValuesAndConfirmsToTheUser() = runTest(dispatcher) {
        val source = StubSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()

        vm.saveProfile(ProfileUpdate("Farmer", "zu", 25, false, false, "+27 82 000 0000"))
        advanceUntilIdle()

        val sent = source.backend.lastUpdate
        assertNotNull("the profile update never reached the API", sent)
        assertEquals("zu", sent!!.language)
        assertEquals(25, sent.searchRadiusKm)
        assertEquals("+27 82 000 0000", sent.phone)
        assertFalse(sent.notificationsEnabled)
        assertEquals("zu", vm.state.value.profile?.language)
        assertEquals("Preferences saved.", vm.state.value.message)
        assertNull(vm.state.value.error)
    }

    @Test fun savingPreferencesMirrorsThisDevicesBiometricEnrolment() = runTest(dispatcher) {
        val source = StubSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()

        // The user switches biometric unlock on, then saves their preferences. The form
        // still carries the value the profile was loaded with, which is now stale.
        vm.enableBiometrics()
        vm.saveProfile(ProfileUpdate("Farmer", "en", 10, true, false, null))
        advanceUntilIdle()

        assertTrue(
            "this device's biometric enrolment was not mirrored into the stored profile",
            source.backend.lastUpdate!!.biometricLockEnabled
        )
    }

    @Test fun aRadiusOutsideTheSupportedRangeIsRejectedWithoutWriting() = runTest(dispatcher) {
        val source = StubSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()

        vm.saveProfile(ProfileUpdate("Farmer", "en", 150, true, false, null))
        advanceUntilIdle()

        assertNull("an out-of-range radius must not be persisted", source.backend.lastUpdate)
        assertNotNull(vm.state.value.error)
    }

    @Test fun switchingToBuyerKeepsTheSameAccountAndUpdatesTheActiveRole() = runTest(dispatcher) {
        val source = StubSource()
        val vm = FarmViewModel(source)
        advanceUntilIdle()

        val userId = vm.state.value.profile!!.userId
        vm.saveProfile(ProfileUpdate("Buyer", "en", 10, true, false, null))
        advanceUntilIdle()

        assertEquals(userId, vm.state.value.profile?.userId)
        assertEquals("Buyer", source.backend.lastUpdate?.role)
        assertEquals("Buyer", vm.state.value.profile?.role)
        assertFalse(vm.state.value.loaded)
    }

    private class StubSource : FarmDataSource {
        val backend = StubApi()
        override val api: FarmApi = backend
        private var biometrics = false
        override fun biometricEnabled() = biometrics
        override fun setBiometricEnabled(enabled: Boolean) { biometrics = enabled }
        override fun hasSession() = true
        override suspend fun signIn(activity: Context) = Unit
        override suspend fun token() = "Bearer test"
        override suspend fun logout() = Unit
    }

    private class StubApi : FarmApi {
        var lastUpdate: ProfileUpdate? = null
        private var stored = Profile("user-one", "Greg", "Farmer", "en", 10, true, false, null)

        override suspend fun session(token: String, body: SessionRequest) = Session("user-one", true)
        override suspend fun profile(token: String) = stored
        override suspend fun profile(token: String, body: ProfileUpdate): Profile {
            lastUpdate = body
            stored = stored.copy(
                role = body.role, language = body.language, searchRadiusKm = body.searchRadiusKm,
                notificationsEnabled = body.notificationsEnabled,
                biometricLockEnabled = body.biometricLockEnabled, phone = body.phone
            )
            return stored
        }
        override suspend fun listings(token: String, mine: Boolean) = emptyList<Listing>()
        override suspend fun browseListings(token: String, crop: String?, radius: Int, latitude: Double, longitude: Double) = emptyList<Listing>()
        override suspend fun createListing(token: String, body: ListingWrite): Listing = error("not used")
        override suspend fun updateListing(token: String, id: String, body: ListingWrite): Listing = error("not used")
        override suspend fun deleteListing(token: String, id: String) = Unit
        override suspend fun demands(token: String, mine: Boolean) = emptyList<Demand>()
        override suspend fun createDemand(token: String, body: DemandWrite): Demand = error("not used")
        override suspend fun updateDemand(token: String, id: String, body: DemandWrite): Demand = error("not used")
        override suspend fun deleteDemand(token: String, id: String) = Unit
        override suspend fun matches(token: String) = emptyList<Match>()
        override suspend fun detail(token: String, id: String): MatchDetail = error("not used")
        override suspend fun confirm(token: String, id: String) = Unit
        override suspend fun complete(token: String, id: String) = Unit
        override suspend fun rate(token: String, id: String, body: RatingWrite) = Unit
    }
}
