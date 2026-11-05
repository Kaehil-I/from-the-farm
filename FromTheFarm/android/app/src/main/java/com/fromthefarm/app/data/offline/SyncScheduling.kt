package com.fromthefarm.app.data.offline

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import com.fromthefarm.app.data.FarmRepository
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.flow.distinctUntilChanged
import java.util.concurrent.TimeUnit

/** Asks the system to upload the queue once the phone has a connection, even if the app is closed. */
interface SyncScheduler { fun schedule() }

class WorkSyncScheduler(private val context: Context) : SyncScheduler {
    // WorkManager keeps the request across app restarts and only runs it while a network is available.
    // https://developer.android.com/develop/background-work/background-tasks/persistent/getting-started
    override fun schedule() {
        val request = OneTimeWorkRequestBuilder<SyncWorker>()
            .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 30, TimeUnit.SECONDS)
            .build()
        // APPEND_OR_REPLACE: a record queued while a sync is already running still gets its own pass.
        WorkManager.getInstance(context.applicationContext)
            .enqueueUniqueWork(WORK_NAME, ExistingWorkPolicy.APPEND_OR_REPLACE, request)
    }
    private companion object { const val WORK_NAME = "outbox-sync" }
}

class SyncWorker(context: Context, params: WorkerParameters) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result {
        val repository = FarmRepository(applicationContext)
        if (!repository.hasSession()) return Result.success()      // queued work waits for the next sign-in
        val owner = repository.currentUserId() ?: return Result.success()
        val store = RoomOfflineStore(FarmDatabase.get(applicationContext).dao())
        val result = OutboxProcessor(store, repository.api).drain(owner) { repository.token() }
        return when (result.outcome) {
            DrainOutcome.DONE, DrainOutcome.NEEDS_SIGN_IN -> Result.success()
            DrainOutcome.RETRY_LATER -> Result.retry()
        }
    }
}

/** True while the phone has a network that can reach the internet. */
fun connectivityFlow(context: Context): Flow<Boolean> = callbackFlow {
    val manager = context.applicationContext.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
    fun online(): Boolean = manager.getNetworkCapabilities(manager.activeNetwork)
        ?.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED) == true
    val callback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) { trySend(online()) }
        override fun onLost(network: Network) { trySend(online()) }
        override fun onCapabilitiesChanged(network: Network, capabilities: NetworkCapabilities) { trySend(online()) }
    }
    trySend(online())
    manager.registerDefaultNetworkCallback(callback)
    awaitClose { manager.unregisterNetworkCallback(callback) }
}.distinctUntilChanged()
