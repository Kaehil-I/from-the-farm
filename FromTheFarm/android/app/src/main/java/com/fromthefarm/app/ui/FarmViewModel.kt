package com.fromthefarm.app.ui

import android.content.Context
import android.util.Log
import androidx.annotation.StringRes
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import androidx.lifecycle.viewModelScope
import com.fromthefarm.app.R
import com.fromthefarm.app.data.*
import com.fromthefarm.app.data.offline.*
import com.google.gson.Gson
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.io.IOException
import java.util.UUID
import java.util.Locale
import androidx.credentials.exceptions.GetCredentialCancellationException

data class FarmState(val busy: Boolean = false, val error: UiText? = null, val message: UiText? = null,
    val firebaseSignedIn: Boolean = false,
    val biometricLocked: Boolean = false, val biometricEnabled: Boolean = false,
    val profile: Profile? = null, val listings: List<Listing> = emptyList(), val nearbyListings: List<Listing> = emptyList(), val demands: List<Demand> = emptyList(),
    val matches: List<Match> = emptyList(), val detail: MatchDetail? = null, val loaded: Boolean = false,
    val ratedMatches: Set<String> = emptySet(), val nearbySearchPerformed: Boolean = false)

class FarmViewModel(private val repository: FarmDataSource,
    private val offline: OfflineSupport = OfflineSupport.none(),
    private val log: (String) -> Unit = {}) : ViewModel() {
    companion object {
        fun factory(context: Context): ViewModelProvider.Factory = viewModelFactory {
            initializer { FarmViewModel(FarmRepository(context.applicationContext), OfflineSupport.forContext(context)) { Log.d("FarmState", it) } }
        }
        private val SAVED_OFFLINE = UiText.Res(R.string.saved_offline)
    }
    private val mutable = MutableStateFlow(FarmState())
    val state = mutable.asStateFlow()

    // Offline sync status for the UI: records waiting to upload, whether the phone is offline, and
    // whether an upload is running. Separate from FarmState because FarmState is rebuilt from scratch
    // on sign-in and sign-out, which would otherwise throw away the queue the person is looking at.
    private val syncMutable = MutableStateFlow(SyncUiState())
    val sync = syncMutable.asStateFlow()
    private val gson = Gson()
    private val processor by lazy { OutboxProcessor(offline.store, repository.api) }
    private var outboxJob: Job? = null

    init {
        // Upload the queue as soon as the connection comes back.
        viewModelScope.launch {
            offline.connectivity.collect { online ->
                syncMutable.value = syncMutable.value.copy(offline = !online)
                if (online) drainOutbox()
            }
        }
    }

    // The match a tapped notification wants opened. Held apart from FarmState because
    // session setup rebuilds FarmState from scratch, which would drop it on a cold start.
    private val pending = MutableStateFlow<String?>(null)
    val pendingMatch = pending.asStateFlow()
    fun openMatchFromNotification(matchId: String?) { if (!matchId.isNullOrBlank()) pending.value = matchId }
    fun consumePendingMatch() { pending.value = null }
    init {
        if (repository.hasSession()) {
            val enabled = repository.biometricEnabled()
            mutable.value = FarmState(firebaseSignedIn = true, biometricEnabled = enabled, biometricLocked = enabled)
            if (!enabled) resumeSession()
        }
    }

    private fun ensure(condition: Boolean, @StringRes message: Int) {
        if (!condition) throw UserFacingException(UiText.Res(message))
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
                    forgetPrivateData(mutable.value.profile?.userId)
                    mutable.value = FarmState(busy = true)
                    try { repository.logout() } catch (cancelled: CancellationException) { throw cancelled }
                    catch (_: Exception) { log("Credential cleanup failed after session rejection") }
                }
                val message = when (e) {
                    is GetCredentialCancellationException -> UiText.Res(R.string.signin_cancelled)
                    is HttpException -> when (e.code()) {
                        401 -> UiText.Res(R.string.err_session_expired)
                        403 -> UiText.Res(R.string.err_forbidden)
                        404 -> UiText.Res(R.string.err_not_found)
                        409 -> UiText.Res(R.string.err_conflict)
                        in 500..599 -> UiText.Res(R.string.err_service_unavailable)
                        else -> UiText.Res(R.string.err_rejected, listOf(e.code()))
                    }
                    is IOException -> UiText.Res(R.string.err_network)
                    is UserFacingException -> e.text
                    // Developer-facing setup problems (missing Firebase or API configuration) stay as written.
                    is IllegalStateException -> e.message?.let { UiText.Raw(it) } ?: UiText.Res(R.string.err_config_incomplete)
                    else -> UiText.Res(R.string.err_generic)
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
        watchOutbox(profile.userId)
        // Not part of the action, so a slow or failing registration never holds the UI busy.
        viewModelScope.launch {
            try { repository.registerDevice() }
            catch (e: CancellationException) { throw e }
            catch (e: Exception) { log("Device registration failed: ${e.javaClass.simpleName}") }
        }
    }
    fun resumeSession() = action { establishSession() }
    fun signIn(context: Context) = action(allowLocked = true) {
        repository.signIn(context)
        log("Signed in with Google; establishing session")
        mutable.value = FarmState(busy = true, firebaseSignedIn = true, biometricEnabled = repository.biometricEnabled())
        establishSession()
    }
    fun biometricError(message: String) { mutable.value = mutable.value.copy(error = UiText.Raw(message)) }
    fun enableBiometrics() {
        if (!repository.hasSession() || mutable.value.busy || mutable.value.biometricLocked) return
        repository.setBiometricEnabled(true)
        log("Biometric lock enabled")
        mutable.value = mutable.value.copy(biometricEnabled = true, error = null, message = UiText.Res(R.string.biometric_enabled_msg))
    }
    fun lock() {
        if (mutable.value.biometricEnabled && repository.hasSession()) {
            mutable.value = mutable.value.copy(biometricLocked = true, error = null, message = null)
            log("App locked")
        }
    }
    fun disableBiometrics() {
        if (mutable.value.busy || mutable.value.biometricLocked || !repository.hasSession()) return
        repository.setBiometricEnabled(false)
        log("Biometric lock disabled")
        mutable.value = mutable.value.copy(biometricEnabled = false, message = UiText.Res(R.string.biometric_disabled_msg))
    }
    fun unlockWithBiometrics() {
        if (!repository.hasSession() || !mutable.value.biometricLocked) return
        mutable.value = mutable.value.copy(biometricLocked = false, error = null, message = UiText.Res(R.string.device_unlocked))
        log("App unlocked with biometrics")
        if (mutable.value.profile == null) resumeSession()
    }
    fun logout() = action(allowLocked = true) {
        forgetPrivateData(mutable.value.profile?.userId)
        mutable.value = FarmState(busy = true)
        repository.logout()
        log("Signed out")
    }
    fun saveProfile(body: ProfileUpdate) = action {
        require(body.role in listOf("Farmer", "Buyer") && body.searchRadiusKm in 1..100)
        // Biometric unlock is enrolled per device, so the device is the source of truth.
        // Mirror it into every profile write; otherwise the stored flag keeps whatever
        // value the form was first rendered with and drifts out of date silently.
        val write = body.copy(biometricLockEnabled = repository.biometricEnabled())
        val profile = repository.api.profile(repository.token(), write)
        mutable.value = mutable.value.copy(profile = profile, loaded = false, message = UiText.Res(R.string.preferences_saved))
    }
    private suspend fun load() {
        val farmer = mutable.value.profile?.role == "Farmer"
        val failures = mutableListOf<UiText>()
        // A teammate's unavailable endpoint must not prevent unrelated screens from loading.
        suspend fun <T> fetch(label: String, what: Int, previous: T, block: suspend () -> T): T = try { block() }
        catch (e: CancellationException) { throw e }
        catch (e: Exception) {
            if (e is HttpException && e.code() == 401) throw e
            log("$label refresh failed: ${e.javaClass.simpleName}")
            failures.add(UiText.Res(what))
            previous
        }
        val owner = mutable.value.profile?.userId
        // Nothing is held in memory after a restart, so the saved copy is what someone without a connection sees.
        val savedListings = mutable.value.listings.ifEmpty { owner?.let { offline.store.cachedListings(it) }.orEmpty() }
        val savedDemands = mutable.value.demands.ifEmpty { owner?.let { offline.store.cachedDemands(it) }.orEmpty() }
        val listings = fetch("listings", R.string.what_listings, savedListings) {
            repository.api.listings(repository.token(), farmer).also { fresh -> owner?.let { offline.store.cacheListings(it, fresh) } }
        }
        val demands = fetch("demand requests", R.string.what_demands, savedDemands) {
            repository.api.demands(repository.token()).also { fresh -> owner?.let { offline.store.cacheDemands(it, fresh) } }
        }
        val matches = fetch("matches", R.string.what_matches, mutable.value.matches) { repository.api.matches(repository.token()).sortedByDescending { it.score } }
        mutable.value = mutable.value.copy(listings = listings, nearbyListings = emptyList(), demands = demands, matches = matches,
            loaded = true, nearbySearchPerformed = false,
            error = failures.takeIf { it.isNotEmpty() }?.let { UiText.Res(R.string.refresh_failed, listOf(UiText.Joined(it))) })
    }
    fun refresh() = action { load() }
    fun browse(crop: String?, radius: Int, location: Location) = action {
        // The deployed Mongo query compares crop text exactly. Fetch the already
        // distance-filtered result set and apply the optional crop filter here so
        // Tomatoes, tomatoes and TOMATOES are treated as the same product.
        val nearby = repository.api.browseListings(repository.token(), null, radius, location.latitude, location.longitude)
        val listings = crop?.let { wanted -> nearby.filter { it.cropType.equals(wanted, ignoreCase = true) } } ?: nearby
        mutable.value = mutable.value.copy(nearbyListings = listings, nearbySearchPerformed = true,
            message = when (listings.size) {
                0 -> UiText.Res(R.string.no_produce_found, listOf(radius))
                1 -> UiText.Res(R.string.found_one_product, listOf(radius))
                else -> UiText.Res(R.string.found_products, listOf(listings.size, radius))
            })
    }
    // A new record gets its id on this phone before the first attempt. If the connection drops, the
    // record is queued under that same id and the API ignores a repeat, so it can only exist once.
    fun saveListing(id: String?, body: ListingWrite, done: () -> Unit) = action {
        var queued = false
        if (id == null) {
            val write = body.copy(clientGeneratedId = body.clientGeneratedId ?: UUID.randomUUID().toString())
            queued = createOrQueue(RecordKind.LISTING, write.clientGeneratedId!!, write, write.cropType,
                "${write.quantity} ${write.unit} · ${write.harvestDate}") { repository.api.createListing(repository.token(), write) }
        } else repository.api.updateListing(repository.token(), id, body)
        mutable.value = if (queued) mutable.value.copy(message = SAVED_OFFLINE) else mutable.value.copy(loaded = false)
        done() // Close only after the server acknowledged the write, or the phone has safely queued it.
    }
    fun saveDemand(id: String?, body: DemandWrite, done: () -> Unit) = action {
        var queued = false
        if (id == null) {
            val write = body.copy(clientGeneratedId = body.clientGeneratedId ?: UUID.randomUUID().toString())
            queued = createOrQueue(RecordKind.DEMAND, write.clientGeneratedId!!, write, write.cropType,
                "${write.quantityNeeded} ${write.unit} · ${write.deadline}") { repository.api.createDemand(repository.token(), write) }
        } else repository.api.updateDemand(repository.token(), id, body)
        if (queued) mutable.value = mutable.value.copy(message = SAVED_OFFLINE)
        else {
            mutable.value = mutable.value.copy(loaded = false)
            load()
            mutable.value = mutable.value.copy(message = UiText.Res(if (id == null) R.string.demand_posted else R.string.demand_updated))
        }
        done()
    }

    /** Sends the create now; if there is no connection, keeps it on the phone to upload later. Returns true when queued. */
    private suspend fun createOrQueue(kind: RecordKind, clientId: String, payload: Any, title: String, detail: String,
        send: suspend () -> Unit): Boolean {
        try { send(); return false }
        catch (e: CancellationException) { throw e }
        catch (e: IOException) {
            val owner = mutable.value.profile?.userId ?: throw e
            offline.store.enqueue(OutboxEntry(ownerId = owner, clientGeneratedId = clientId, kind = kind.name,
                payload = gson.toJson(payload), title = title, detail = detail, createdAt = System.currentTimeMillis()))
            offline.scheduler.schedule()
            log("Record queued for upload")
            return true
        }
    }

    // ---- Offline sync ----
    private fun watchOutbox(owner: String) {
        outboxJob?.cancel()
        outboxJob = viewModelScope.launch {
            offline.store.observeOutbox(owner).collect { entries ->
                syncMutable.value = syncMutable.value.copy(records = entries.map { it.toPending() })
            }
        }
        viewModelScope.launch { drainOutbox() }
    }
    /** Upload everything queued now. Safe to call at any time; does nothing when the queue is empty. */
    fun syncNow() { viewModelScope.launch { drainOutbox() } }
    private suspend fun drainOutbox() {
        val owner = mutable.value.profile?.userId ?: return
        if (syncMutable.value.syncing) return
        syncMutable.value = syncMutable.value.copy(syncing = true)
        try {
            val result = processor.drain(owner) { repository.token() }
            log("Outbox drain finished: ${result.uploaded} uploaded, ${result.outcome}")
            if (result.uploaded > 0) mutable.value = mutable.value.copy(loaded = false,
                message = if (result.uploaded == 1) UiText.Res(R.string.uploaded_one_record)
                    else UiText.Res(R.string.uploaded_records, listOf(result.uploaded)))
            when (result.outcome) {
                DrainOutcome.RETRY_LATER -> offline.scheduler.schedule() // also covers the app being closed before it works
                DrainOutcome.NEEDS_SIGN_IN -> mutable.value = mutable.value.copy(error = UiText.Res(R.string.sign_in_again_upload))
                DrainOutcome.DONE -> Unit
            }
        } catch (e: CancellationException) { throw e }
        catch (e: Exception) { log("Sync failed: ${e.javaClass.simpleName}") }
        finally { syncMutable.value = syncMutable.value.copy(syncing = false) }
    }
    /** Gives a failed record another go. */
    fun retryRecord(clientId: String) {
        val owner = mutable.value.profile?.userId ?: return
        viewModelScope.launch {
            offline.store.find(owner, clientId)?.let {
                offline.store.update(it.copy(status = SyncStatus.PENDING.name, attempts = 0, lastError = null))
            }
            drainOutbox()
        }
    }
    /** Throws a queued or failed record away without uploading it. */
    fun discardRecord(clientId: String) {
        val owner = mutable.value.profile?.userId ?: return
        viewModelScope.launch { offline.store.find(owner, clientId)?.let { offline.store.remove(it.id) } }
    }
    // Signing out wipes the readable copy of server data. Unsent records stay on the phone for the
    // same account's next sign-in, but stop being shown here.
    private suspend fun forgetPrivateData(owner: String?) {
        outboxJob?.cancel()
        syncMutable.value = SyncUiState(offline = syncMutable.value.offline)
        if (owner == null) return
        try { offline.store.clearCache(owner) }
        catch (e: CancellationException) { throw e }
        catch (_: Exception) { log("Could not clear the saved copy") }
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
        ensure(mutable.value.detail?.let { it.matchId == id && it.status == "Suggested" } == true, R.string.err_refresh_before_confirm)
        repository.api.confirm(repository.token(), id)
        log("Match confirmed")
        mutable.value = mutable.value.copy(detail = mutable.value.detail?.copy(status = "Confirmed"), loaded = false)
        mutable.value = mutable.value.copy(detail = repository.api.detail(repository.token(), id), loaded = false)
    }
    fun complete(id: String) = action {
        ensure(mutable.value.detail?.let { it.matchId == id && it.status == "Confirmed" } == true, R.string.err_only_confirmed_complete)
        repository.api.complete(repository.token(), id)
        log("Match marked completed")
        // The write succeeded even if the following detail refresh fails.
        mutable.value = mutable.value.copy(detail = mutable.value.detail?.copy(status = "Completed"),
            loaded = false, message = UiText.Res(R.string.exchange_completed))
        mutable.value = mutable.value.copy(detail = repository.api.detail(repository.token(), id))
    }
    fun rate(id: String, positive: Boolean) = action {
        ensure(mutable.value.detail?.let { it.matchId == id && it.status == "Completed" } == true, R.string.err_only_completed_rate)
        ensure(id !in mutable.value.ratedMatches, R.string.err_already_rated)
        repository.api.rate(repository.token(), id, RatingWrite(positive))
        log("Rating submitted")
        mutable.value = mutable.value.copy(message = UiText.Res(R.string.rating_submitted), ratedMatches = mutable.value.ratedMatches + id)
    }
}
