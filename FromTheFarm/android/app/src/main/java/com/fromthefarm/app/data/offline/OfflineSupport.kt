package com.fromthefarm.app.data.offline

import android.content.Context
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf

/** The three things the view model needs for offline work, bundled so tests can swap them out. */
class OfflineSupport(
    val store: OfflineStore,
    val scheduler: SyncScheduler,
    /** Emits true while the phone has a working connection. */
    val connectivity: Flow<Boolean>
) {
    companion object {
        fun forContext(context: Context): OfflineSupport {
            val app = context.applicationContext
            return OfflineSupport(RoomOfflineStore(FarmDatabase.get(app).dao()), WorkSyncScheduler(app), connectivityFlow(app))
        }

        /** In-memory, always online, never schedules anything. */
        fun none() = OfflineSupport(InMemoryOfflineStore(), object : SyncScheduler { override fun schedule() {} }, flowOf(true))
    }
}
