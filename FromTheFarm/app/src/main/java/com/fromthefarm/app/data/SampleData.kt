package com.fromthefarm.app.data

data class Listing(
    val id: String,
    val cropName: String,
    val quantityKg: Int,
    val harvestDate: String,
    val distanceKm: Double,
    val status: String, // "Active" | "Matched" | "Expired"
    val ownerName: String = ""
)

data class DemandRequest(
    val id: String,
    val cropName: String,
    val quantityKg: Int,
    val neededBy: String,
    val distanceKm: Double,
    val postedByFarmName: String = ""
)

data class CalendarEvent(
    val date: String,
    val label: String,
    val isSupply: Boolean // true = listing ready, false = demand needed
)

// Sample/mock data — replace with real API calls once Kaehil's endpoints are live.
object SampleData {

    val myListings = listOf(
        Listing("l1", "Tomatoes", 50, "3 Sept", 0.0, "Matched"),
        Listing("l2", "Carrots", 30, "10 Sept", 0.0, "Active"),
        Listing("l3", "Spinach", 20, "20 Aug", 0.0, "Expired")
    )

    val nearbyMatches = listOf(
        Listing("m1", "Tomatoes", 50, "3 Sept", 2.3, "Active", ownerName = "Zanele's farm"),
        Listing("m2", "Carrots", 30, "10 Sept", 4.1, "Active", ownerName = "Sipho's plot"),
        Listing("m3", "Peppers", 15, "12 Sept", 6.8, "Active", ownerName = "Nomvula's farm")
    )

    val demandBoard = listOf(
        DemandRequest("d1", "Tomatoes", 50, "5 Sept", 2.3, "Zanele's farm"),
        DemandRequest("d2", "Carrots", 30, "12 Sept", 4.1, "Sipho's plot")
    )

    val calendarEvents = listOf(
        CalendarEvent("3 Sept", "Tomatoes ready · 50kg", isSupply = true),
        CalendarEvent("15 Sept", "Spinach needed · 40kg", isSupply = false)
    )
}
