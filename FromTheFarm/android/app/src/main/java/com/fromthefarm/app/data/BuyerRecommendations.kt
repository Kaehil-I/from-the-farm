package com.fromthefarm.app.data

import kotlin.math.*

data class BuyerRecommendation(val listing: Listing, val demand: Demand, val distanceKm: Double)
data class NearbyBuyerListing(val listing: Listing, val distanceKm: Double)

object BuyerRecommendations {
    fun find(listings: List<Listing>, demands: List<Demand>): List<BuyerRecommendation> = listings.mapNotNull { listing ->
        demands.asSequence()
            .filter { it.status == "Open" && it.cropType.equals(listing.cropType, ignoreCase = true) }
            .map { BuyerRecommendation(listing, it, distanceKm(listing.location, it.location)) }
            .minByOrNull { it.distanceKm }
    }.sortedWith(compareBy<BuyerRecommendation> { it.distanceKm }.thenBy { it.listing.cropType.lowercase() })

    fun nearby(listings: List<Listing>, demands: List<Demand>, radiusKm: Int,
        excludedListingIds: Set<String> = emptySet()): List<NearbyBuyerListing> {
        val locations = demands.filter { it.status == "Open" }.map { it.location }
        if (locations.isEmpty()) return emptyList()
        return listings.asSequence()
            .filterNot { it.id in excludedListingIds }
            .map { listing -> NearbyBuyerListing(listing, locations.minOf { distanceKm(listing.location, it) }) }
            .filter { it.distanceKm <= radiusKm }
            .sortedWith(compareBy<NearbyBuyerListing> { it.distanceKm }.thenBy { it.listing.cropType.lowercase() })
            .toList()
    }

    private fun distanceKm(first: Location, second: Location): Double {
        val lat = Math.toRadians(second.latitude - first.latitude)
        val lon = Math.toRadians(second.longitude - first.longitude)
        val a = sin(lat / 2).pow(2) + cos(Math.toRadians(first.latitude)) * cos(Math.toRadians(second.latitude)) * sin(lon / 2).pow(2)
        return 6371.0 * 2 * atan2(sqrt(a), sqrt(1 - a))
    }
}
