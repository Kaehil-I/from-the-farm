package com.fromthefarm.app.ui

import android.content.Context
import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import androidx.lifecycle.viewModelScope
import com.fromthefarm.app.data.*
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.io.IOException
import java.util.Locale
import androidx.credentials.exceptions.GetCredentialCancellationException

data class FarmState(val busy: Boolean = false, val error: String? = null, val message: String? = null,
    val firebaseSignedIn: Boolean = false,
    val biometricLocked: Boolean = false, val biometricEnabled: Boolean = false,
    val profile: Profile? = null, val listings: List<Listing> = emptyList(), val demands: List<Demand> = emptyList(),
    val matches: List<Match> = emptyList(), val detail: MatchDetail? = null, val loaded: Boolean = false,
    val ratedMatches: Set<String> = emptySet())

class FarmViewModel(private val repository: FarmDataSource, private val log: (String) -> Unit = {}) : ViewModel() {
    companion object {
        fun factory(context: Context): ViewModelProvider.Factory = viewModelFactory {
            initializer { FarmViewModel(FarmRepository(context.applicationContext)) { Log.d("FarmState", it) } }
        }
    }
    private val mutable = MutableStateFlow(FarmState())
    val state = mutable.asStateFlow()
    init {
        if (repository.hasSession()) {
            val enabled = repository.biometricEnabled()
            mutable.value = FarmState(firebaseSignedIn = true, biometricEnabled = enabled, biometricLocked = enabled)
            if (!enabled) resumeSession()
        }
    }

    // Serialize user actions, retain form state on errors, and never log tokens or personal data.
    private fun action(allowLocked: Boolean = false, block: suspend () -> Unit) {
        if (mutable.value.busy || (mutable.value.biometricLocked && !allowLocked)) return
        mutable.value = mutable.value.copy(busy = true, error = null, message = null)
        viewModelScope.launch {
            try { block(); log("Action completed") }
            catch (e: CancellationException) { throw e }
            catch (e: Exception) {
                log("Action failed: ${e.javaClass.simpleName}")
                if (e is HttpException && e.code() == 401) {
                    mutable.value = FarmState(busy = true)
                    try { repository.logout() } catch (cancelled: CancellationException) { throw cancelled }
                    catch (_: Exception) { log("Credential cleanup failed after session rejection") }
                }
                val message = when (e) {
                    is GetCredentialCancellationException -> "Sign-in cancelled. You can try again when ready."
                    is HttpException -> when (e.code()) {
                        401 -> "Your session expired or was rejected. Please sign in again."
                        403 -> "You do not have permission for this action."
                        404 -> "This record is no longer available. Refresh and try again."
                        409 -> "This action was already recorded. Refresh to check its state."
                        in 500..599 -> "The farm service is unavailable. Please try again."
                        else -> "The service rejected the request (${e.code()}). Check your inputs."
                    }
                    is IOException -> "Cannot reach the service. Check your connection and retry."
                    is IllegalStateException -> e.message ?: "Configuration is incomplete."
                    else -> "The action could not complete. Please try again."
                }
                mutable.value = mutable.value.copy(error = message)
            } finally { mutable.value = mutable.value.copy(busy = false) }
        }
    }
    private suspend fun establishSession() {
        mutable.value = mutable.value.copy(firebaseSignedIn = repository.hasSession())
        repository.api.session(repository.token(), SessionRequest(Locale.getDefault().language))
        val profile = repository.api.profile(repository.token())
        mutable.value = FarmState(busy = true, firebaseSignedIn = true, profile = profile,
            biometricEnabled = repository.biometricEnabled(), biometricLocked = mutable.value.biometricLocked)
    }
    fun resumeSession() = action { establishSession() }
    fun signIn(context: Context) = action(allowLocked = true) {
        repository.signIn(context)
        mutable.value = FarmState(busy = true, firebaseSignedIn = true, biometricEnabled = repository.biometricEnabled())
        establishSession()
    }
    fun biometricError(message: String) { mutable.value = mutable.value.copy(error = message) }
    fun enableBiometrics() {
        if (!repository.hasSession() || mutable.value.busy || mutable.value.biometricLocked) return
        repository.setBiometricEnabled(true)
        mutable.value = mutable.value.copy(biometricEnabled = true, error = null, message = "Biometric unlock enabled on this device.")
    }
    fun lock() {
        if (mutable.value.biometricEnabled && repository.hasSession())
            mutable.value = mutable.value.copy(biometricLocked = true, error = null, message = null)
    }
    fun disableBiometrics() {
        if (mutable.value.busy || mutable.value.biometricLocked || !repository.hasSession()) return
        repository.setBiometricEnabled(false)
        mutable.value = mutable.value.copy(biometricEnabled = false, message = "Biometric unlock disabled on this device.")
    }
    fun unlockWithBiometrics() {
        if (!repository.hasSession() || !mutable.value.biometricLocked) return
        mutable.value = mutable.value.copy(biometricLocked = false, error = null, message = "Device unlocked.")
        if (mutable.value.profile == null) resumeSession()
    }
    fun logout() = action(allowLocked = true) {
        mutable.value = FarmState(busy = true)
        repository.logout()
    }
    fun saveProfile(body: ProfileUpdate) = action {
        require(body.role in listOf("Farmer", "Buyer") && body.searchRadiusKm in 1..100)
        // Biometric unlock is enrolled per device, so the device is the source of truth.
        // Mirror it into every profile write; otherwise the stored flag keeps whatever
        // value the form was first rendered with and drifts out of date silently.
        val write = body.copy(biometricLockEnabled = repository.biometricEnabled())
        val profile = repository.api.profile(repository.token(), write)
        mutable.value = mutable.value.copy(profile = profile, loaded = false, message = "Preferences saved.")
    }
    private suspend fun load() {
        val farmer = mutable.value.profile?.role == "Farmer"
        val failures = mutableListOf<String>()
        // A teammate's unavailable endpoint must not prevent unrelated screens from loading.
        suspend fun <T> fetch(label: String, previous: T, block: suspend () -> T): T = try { block() }
        catch (e: CancellationException) { throw e }
        catch (e: Exception) {
            if (e is HttpException && e.code() == 401) throw e
            log("$label refresh failed: ${e.javaClass.simpleName}")
            failures.add(label)
            previous
        }
        val listings = fetch("listings", mutable.value.listings) { repository.api.listings(repository.token(), farmer) }
        val demands = fetch("demand requests", mutable.value.demands) { repository.api.demands(repository.token()) }
        val matches = fetch("matches", mutable.value.matches) { repository.api.matches(repository.token()).sortedByDescending { it.score } }
        mutable.value = mutable.value.copy(listings = listings, demands = demands, matches = matches, loaded = true,
            error = failures.takeIf { it.isNotEmpty() }?.let { "Could not refresh ${it.joinToString()}. Previously loaded data may be out of date. Retry with Refresh." })
    }
    fun refresh() = action { load() }
    fun browse(crop: String?, radius: Int, location: Location) = action {
        val listings = repository.api.browseListings(repository.token(), crop, radius, location.latitude, location.longitude)
        mutable.value = mutable.value.copy(listings = listings, message = "Showing produce within $radius km.")
    }
    fun saveListing(id: String?, body: ListingWrite, done: () -> Unit) = action {
        if (id == null) repository.api.createListing(repository.token(), body)
        else repository.api.updateListing(repository.token(), id, body)
        mutable.value = mutable.value.copy(loaded = false)
        done() // Close only after the server acknowledged the write.
    }
    fun saveDemand(id: String?, body: DemandWrite, done: () -> Unit) = action {
        if (id == null) repository.api.createDemand(repository.token(), body)
        else repository.api.updateDemand(repository.token(), id, body)
        mutable.value = mutable.value.copy(loaded = false)
        done()
    }
    fun delete(id: String, demand: Boolean) = action {
        if (demand) repository.api.deleteDemand(repository.token(), id) else repository.api.deleteListing(repository.token(), id)
        mutable.value = mutable.value.copy(loaded = false)
    }
    fun openMatch(id: String) {
        if (mutable.value.busy) return
        mutable.value = mutable.value.copy(detail = null)
        action { mutable.value = mutable.value.copy(detail = repository.api.detail(repository.token(), id)) }
    }
    fun confirm(id: String) = action {
        check(mutable.value.detail?.let { it.matchId == id && it.status == "Suggested" } == true) { "Refresh this match before confirming it." }
        repository.api.confirm(repository.token(), id)
        mutable.value = mutable.value.copy(detail = mutable.value.detail?.copy(status = "Confirmed"), loaded = false)
        mutable.value = mutable.value.copy(detail = repository.api.detail(repository.token(), id), loaded = false)
    }
    fun complete(id: String) = action {
        check(mutable.value.detail?.let { it.matchId == id && it.status == "Confirmed" } == true) { "Only confirmed exchanges can be completed." }
        repository.api.complete(repository.token(), id)
        // The write succeeded even if the following detail refresh fails.
        mutable.value = mutable.value.copy(detail = mutable.value.detail?.copy(status = "Completed"),
            loaded = false, message = "Exchange marked completed.")
        mutable.value = mutable.value.copy(detail = repository.api.detail(repository.token(), id))
    }
    fun rate(id: String, positive: Boolean) = action {
        check(mutable.value.detail?.let { it.matchId == id && it.status == "Completed" } == true) { "Only completed matches can be rated." }
        check(id !in mutable.value.ratedMatches) { "You have already rated this match." }
        repository.api.rate(repository.token(), id, RatingWrite(positive))
        mutable.value = mutable.value.copy(message = "Rating submitted.", ratedMatches = mutable.value.ratedMatches + id)
    }
}
