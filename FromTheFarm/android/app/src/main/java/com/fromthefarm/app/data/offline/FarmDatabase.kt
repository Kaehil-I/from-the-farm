package com.fromthefarm.app.data.offline

import android.content.Context
import androidx.room.Dao
import androidx.room.Database
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.Transaction
import androidx.room.Update
import kotlinx.coroutines.flow.Flow

// Room DAO pattern: https://developer.android.com/training/data-storage/room/accessing-data
@Dao
abstract class FarmDao {
    // ---- Cached server data, replaced wholesale after each successful fetch ----
    @Query("SELECT * FROM listings WHERE ownerId = :owner") abstract suspend fun listings(owner: String): List<ListingEntity>
    @Query("DELETE FROM listings WHERE ownerId = :owner") abstract suspend fun clearListings(owner: String)
    @Insert(onConflict = OnConflictStrategy.REPLACE) abstract suspend fun insertListings(items: List<ListingEntity>)
    @Transaction open suspend fun replaceListings(owner: String, items: List<ListingEntity>) {
        clearListings(owner); insertListings(items)
    }

    @Query("SELECT * FROM demands WHERE ownerId = :owner") abstract suspend fun demands(owner: String): List<DemandEntity>
    @Query("DELETE FROM demands WHERE ownerId = :owner") abstract suspend fun clearDemands(owner: String)
    @Insert(onConflict = OnConflictStrategy.REPLACE) abstract suspend fun insertDemands(items: List<DemandEntity>)
    @Transaction open suspend fun replaceDemands(owner: String, items: List<DemandEntity>) {
        clearDemands(owner); insertDemands(items)
    }

    // ---- Outbox ----
    // IGNORE: the unique clientGeneratedId means queueing the same record twice is a no-op (returns -1).
    @Insert(onConflict = OnConflictStrategy.IGNORE) abstract suspend fun addToOutbox(entry: OutboxEntry): Long
    @Query("SELECT * FROM outbox WHERE ownerId = :owner ORDER BY createdAt, id")
    abstract fun observeOutbox(owner: String): Flow<List<OutboxEntry>>
    @Query("SELECT * FROM outbox WHERE ownerId = :owner AND status = 'PENDING' ORDER BY createdAt, id")
    abstract suspend fun pending(owner: String): List<OutboxEntry>
    @Query("SELECT * FROM outbox WHERE ownerId = :owner AND clientGeneratedId = :clientId LIMIT 1")
    abstract suspend fun find(owner: String, clientId: String): OutboxEntry?
    @Update abstract suspend fun updateOutbox(entry: OutboxEntry)
    @Query("DELETE FROM outbox WHERE id = :id") abstract suspend fun removeFromOutbox(id: Long)
}

// exportSchema is off and there is no migration yet: this is schema version 1. The first change
// to a table after release needs a real Migration, because the outbox holds unsent user data.
@Database(entities = [ListingEntity::class, DemandEntity::class, OutboxEntry::class], version = 1, exportSchema = false)
abstract class FarmDatabase : RoomDatabase() {
    abstract fun dao(): FarmDao

    companion object {
        @Volatile private var instance: FarmDatabase? = null
        fun get(context: Context): FarmDatabase = instance ?: synchronized(this) {
            instance ?: Room.databaseBuilder(context.applicationContext, FarmDatabase::class.java, "from_the_farm.db")
                .build().also { instance = it }
        }
    }
}
