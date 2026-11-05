package com.fromthefarm.app.data.offline

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey
import com.fromthefarm.app.data.Demand
import com.fromthefarm.app.data.Listing
import com.fromthefarm.app.data.Location

// ---- What the UI sees --------------------------------------------------------------

enum class RecordKind { LISTING, DEMAND }

/** SYNCED records are the ones the server already has; only PENDING and FAILED ever appear in the outbox. */
enum class SyncStatus { SYNCED, PENDING, FAILED }

/** A record created on this phone that the server has not accepted yet. */
data class PendingRecord(
    val clientGeneratedId: String,
    val kind: RecordKind,
    val title: String,
    val detail: String,
    val status: SyncStatus,
    val lastError: String?
)

/** Everything an offline indicator needs, kept apart from FarmState so a session reset cannot wipe it. */
data class SyncUiState(
    val offline: Boolean = false,
    val syncing: Boolean = false,
    val records: List<PendingRecord> = emptyList()
) {
    val pendingCount: Int get() = records.count { it.status == SyncStatus.PENDING }
    val failedCount: Int get() = records.count { it.status == SyncStatus.FAILED }
}

// ---- What Room stores --------------------------------------------------------------

/**
 * One create operation waiting to be sent. The same clientGeneratedId is used for every retry,
 * and the API returns the original record when it sees an id twice, so a retry after a dropped
 * connection can never produce a second record. See the unique index in MongoIndexes.cs.
 */
@Entity(
    tableName = "outbox",
    indices = [Index(value = ["clientGeneratedId"], unique = true), Index(value = ["ownerId"])]
)
data class OutboxEntry(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val ownerId: String,
    val clientGeneratedId: String,
    val kind: String,          // RecordKind name
    val payload: String,       // JSON of the ListingWrite or DemandWrite, clientGeneratedId included
    val title: String,         // shown in the UI, e.g. "Tomatoes"
    val detail: String,        // e.g. "12.5 kg · 2026-09-16"
    val status: String = SyncStatus.PENDING.name,
    val attempts: Int = 0,
    val lastError: String? = null,
    val createdAt: Long
) {
    fun toPending() = PendingRecord(clientGeneratedId, RecordKind.valueOf(kind), title, detail,
        SyncStatus.valueOf(status), lastError)
}

/** Last listings the server returned for this user, so the app has something to show offline. */
@Entity(tableName = "listings", indices = [Index(value = ["ownerId"])])
data class ListingEntity(
    @PrimaryKey val id: String,
    val ownerId: String,
    val cropType: String,
    val quantity: Double,
    val unit: String,
    val harvestDate: String,
    val latitude: Double,
    val longitude: Double,
    val status: String,
    val photoUrl: String?
) {
    fun toListing() = Listing(id, cropType, quantity, unit, harvestDate, Location(latitude, longitude), status, photoUrl)
    companion object {
        fun of(owner: String, l: Listing) = ListingEntity(l.id, owner, l.cropType, l.quantity, l.unit, l.harvestDate,
            l.location.latitude, l.location.longitude, l.status, l.photoUrl)
    }
}

@Entity(tableName = "demands", indices = [Index(value = ["ownerId"])])
data class DemandEntity(
    @PrimaryKey val id: String,
    val ownerId: String,
    val cropType: String,
    val quantityNeeded: Double,
    val unit: String,
    val deadline: String,
    val latitude: Double,
    val longitude: Double,
    val status: String
) {
    fun toDemand() = Demand(id, cropType, quantityNeeded, unit, deadline, Location(latitude, longitude), status)
    companion object {
        fun of(owner: String, d: Demand) = DemandEntity(d.id, owner, d.cropType, d.quantityNeeded, d.unit, d.deadline,
            d.location.latitude, d.location.longitude, d.status)
    }
}
