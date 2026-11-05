package com.fromthefarm.app.data.offline

import com.fromthefarm.app.data.Demand
import com.fromthefarm.app.data.Listing
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.map

/**
 * Everything the app keeps on the phone. The view model and the sync worker only talk to this
 * interface, so the queue logic can be unit tested on the JVM without a Room database.
 * Every call is scoped to an owner so one person's data is never read for another account.
 */
interface OfflineStore {
    suspend fun cacheListings(owner: String, items: List<Listing>)
    suspend fun cachedListings(owner: String): List<Listing>
    suspend fun cacheDemands(owner: String, items: List<Demand>)
    suspend fun cachedDemands(owner: String): List<Demand>
    suspend fun clearCache(owner: String)

    /** Returns false when this clientGeneratedId is already queued. */
    suspend fun enqueue(entry: OutboxEntry): Boolean
    fun observeOutbox(owner: String): Flow<List<OutboxEntry>>
    suspend fun pending(owner: String): List<OutboxEntry>
    suspend fun find(owner: String, clientId: String): OutboxEntry?
    suspend fun update(entry: OutboxEntry)
    suspend fun remove(id: Long)
}

class RoomOfflineStore(private val dao: FarmDao) : OfflineStore {
    override suspend fun cacheListings(owner: String, items: List<Listing>) =
        dao.replaceListings(owner, items.map { ListingEntity.of(owner, it) })
    override suspend fun cachedListings(owner: String) = dao.listings(owner).map { it.toListing() }
    override suspend fun cacheDemands(owner: String, items: List<Demand>) =
        dao.replaceDemands(owner, items.map { DemandEntity.of(owner, it) })
    override suspend fun cachedDemands(owner: String) = dao.demands(owner).map { it.toDemand() }
    // The outbox is deliberately kept: it is unsent work that belongs to this person and uploads
    // the next time they are signed in. Only the readable copy of server data is wiped.
    override suspend fun clearCache(owner: String) { dao.clearListings(owner); dao.clearDemands(owner) }

    override suspend fun enqueue(entry: OutboxEntry) = dao.addToOutbox(entry) != -1L
    override fun observeOutbox(owner: String) = dao.observeOutbox(owner)
    override suspend fun pending(owner: String) = dao.pending(owner)
    override suspend fun find(owner: String, clientId: String) = dao.find(owner, clientId)
    override suspend fun update(entry: OutboxEntry) = dao.updateOutbox(entry)
    override suspend fun remove(id: Long) = dao.removeFromOutbox(id)
}

/** Keeps everything in memory. Used by unit tests and as the do-nothing default for the view model. */
class InMemoryOfflineStore : OfflineStore {
    private val listings = MutableStateFlow<Map<String, List<Listing>>>(emptyMap())
    private val demands = MutableStateFlow<Map<String, List<Demand>>>(emptyMap())
    private val outbox = MutableStateFlow<List<OutboxEntry>>(emptyList())
    private var nextId = 1L

    override suspend fun cacheListings(owner: String, items: List<Listing>) { listings.value = listings.value + (owner to items) }
    override suspend fun cachedListings(owner: String) = listings.value[owner].orEmpty()
    override suspend fun cacheDemands(owner: String, items: List<Demand>) { demands.value = demands.value + (owner to items) }
    override suspend fun cachedDemands(owner: String) = demands.value[owner].orEmpty()
    override suspend fun clearCache(owner: String) { listings.value = listings.value - owner; demands.value = demands.value - owner }

    override suspend fun enqueue(entry: OutboxEntry): Boolean {
        if (outbox.value.any { it.clientGeneratedId == entry.clientGeneratedId }) return false
        outbox.value = outbox.value + entry.copy(id = nextId++)
        return true
    }
    private fun ordered(owner: String) = outbox.value.filter { it.ownerId == owner }.sortedWith(compareBy({ it.createdAt }, { it.id }))
    override fun observeOutbox(owner: String) = outbox.map { ordered(owner) }
    override suspend fun pending(owner: String) = ordered(owner).filter { it.status == SyncStatus.PENDING.name }
    override suspend fun find(owner: String, clientId: String) = ordered(owner).firstOrNull { it.clientGeneratedId == clientId }
    override suspend fun update(entry: OutboxEntry) { outbox.value = outbox.value.map { if (it.id == entry.id) entry else it } }
    override suspend fun remove(id: Long) { outbox.value = outbox.value.filterNot { it.id == id } }
}
