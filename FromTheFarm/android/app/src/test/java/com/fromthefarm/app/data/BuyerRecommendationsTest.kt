package com.fromthefarm.app.data

import org.junit.Assert.*
import org.junit.Test

class BuyerRecommendationsTest {
    private val near = Location(-26.2041, 28.0473)

    @Test fun cropMatchingIsCaseInsensitive() {
        val listing = Listing("l1", "Tomatoes", 20.0, "kg", "2026-09-25", near, "Active")
        val demand = Demand("d1", "tOMAToes", 10.0, "kg", "2026-09-26", near, "Open")

        val result = BuyerRecommendations.find(listOf(listing), listOf(demand))

        assertEquals(listing, result.single().listing)
    }

    @Test fun unrelatedListingsAreExcludedAndDistantCropMatchesRemainVisible() {
        val matching = Listing("l1", "Tomatoes", 20.0, "kg", "2026-09-25", near, "Active")
        val unrelated = matching.copy(id = "l2", cropType = "Potatoes")
        val distant = matching.copy(id = "l3", location = Location(-33.9249, 18.4241))
        val demand = Demand("d1", "tomatoes", 10.0, "kg", "2026-09-26", near, "Open")

        val result = BuyerRecommendations.find(listOf(matching, unrelated, distant), listOf(demand))

        assertEquals(listOf("l1", "l3"), result.map { it.listing.id })
    }

    @Test fun nearbyProductsUseDemandLocationAndExcludeTopRecommendations() {
        val matching = Listing("l1", "Tomatoes", 20.0, "kg", "2026-09-25", near, "Active")
        val nearby = matching.copy(id = "l2", cropType = "Potatoes", location = Location(-26.2050, 28.0473))
        val distant = matching.copy(id = "l3", cropType = "Spinach", location = Location(-33.9249, 18.4241))
        val demand = Demand("d1", "tomatoes", 10.0, "kg", "2026-09-26", near, "Open")

        val result = BuyerRecommendations.nearby(listOf(matching, nearby, distant), listOf(demand), 25, setOf("l1"))

        assertEquals(listOf("l2"), result.map { it.listing.id })
    }

    @Test fun nearbyProductsNeedAnOpenDemandLocation() {
        val listing = Listing("l1", "Kale", 10.0, "kg", "2026-09-25", near, "Active")
        val closed = Demand("d1", "Kale", 10.0, "kg", "2026-09-26", near, "Closed")

        assertTrue(BuyerRecommendations.nearby(listOf(listing), listOf(closed), 25).isEmpty())
    }
}
