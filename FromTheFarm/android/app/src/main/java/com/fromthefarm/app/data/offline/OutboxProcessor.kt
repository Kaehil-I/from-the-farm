package com.fromthefarm.app.data.offline

import com.fromthefarm.app.data.DemandWrite
import com.fromthefarm.app.data.FarmApi
import com.fromthefarm.app.data.ListingWrite
import com.google.gson.Gson
import com.google.gson.JsonParseException
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import retrofit2.HttpException
import java.io.IOException

enum class DrainOutcome {
    /** Nothing left that can be sent right now. Failed items may remain for the user to look at. */
    DONE,
    /** The network dropped or the service is down. Everything unsent stays queued; try again later. */
    RETRY_LATER,
    /** The server rejected the login. Nothing is lost; uploading resumes after the next sign-in. */
    NEEDS_SIGN_IN
}

data class DrainResult(val uploaded: Int, val outcome: DrainOutcome)

/**
 * Sends queued creates to the API, oldest first. Safe to run from the app and from the background
 * worker at the same time: a process-wide lock lets one drain at a time through, and the API
 * itself ignores a clientGeneratedId it has already stored.
 */
class OutboxProcessor(
    private val store: OfflineStore,
    private val api: FarmApi,
    private val gson: Gson = Gson(),
    private val maxAttempts: Int = 5
) {
    suspend fun drain(owner: String, token: suspend () -> String): DrainResult = lock.withLock {
        var uploaded = 0
        for (entry in store.pending(owner)) {
            try {
                when (RecordKind.valueOf(entry.kind)) {
                    RecordKind.LISTING -> api.createListing(token(), gson.fromJson(entry.payload, ListingWrite::class.java))
                    RecordKind.DEMAND -> api.createDemand(token(), gson.fromJson(entry.payload, DemandWrite::class.java))
                }
                store.remove(entry.id)
                uploaded++
            } catch (e: CancellationException) {
                throw e
            } catch (e: IOException) {
                // No connection (or the request may or may not have arrived). Not the record's fault,
                // so the attempt is not counted. Re-sending is safe because of clientGeneratedId.
                return DrainResult(uploaded, DrainOutcome.RETRY_LATER)
            } catch (e: HttpException) {
                when (val code = e.code()) {
                    401 -> return DrainResult(uploaded, DrainOutcome.NEEDS_SIGN_IN)
                    408, 429, in 500..599 -> {
                        val attempts = entry.attempts + 1
                        val message = "The farm service was unavailable ($code)."
                        if (attempts >= maxAttempts) store.update(entry.copy(attempts = attempts, status = SyncStatus.FAILED.name, lastError = message))
                        else { store.update(entry.copy(attempts = attempts, lastError = message)); return DrainResult(uploaded, DrainOutcome.RETRY_LATER) }
                    }
                    // The server understood and refused (validation, ownership, ...). Retrying cannot help.
                    else -> store.update(entry.copy(attempts = entry.attempts + 1, status = SyncStatus.FAILED.name,
                        lastError = "The service rejected this record ($code). Check its details and add it again."))
                }
            } catch (e: JsonParseException) {
                store.update(entry.copy(status = SyncStatus.FAILED.name, lastError = "This saved record could not be read."))
            } catch (e: IllegalStateException) {
                // Raised by the token provider when the Firebase session is gone.
                return DrainResult(uploaded, DrainOutcome.NEEDS_SIGN_IN)
            }
        }
        DrainResult(uploaded, DrainOutcome.DONE)
    }

    private companion object { val lock = Mutex() }
}
