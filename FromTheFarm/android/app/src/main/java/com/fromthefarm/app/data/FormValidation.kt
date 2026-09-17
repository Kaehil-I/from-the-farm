package com.fromthefarm.app.data

import java.time.LocalDate

object FormValidation {
    fun validate(crop: String, quantity: String, unit: String, date: String,
        latitude: String, longitude: String, demand: Boolean, today: LocalDate = LocalDate.now()): String? {
        if (crop.isBlank() || crop.length > 100) return "Enter a crop name (1–100 characters)."
        val amount = quantity.toDoubleOrNull()
        if (amount == null || !amount.isFinite() || amount <= 0 || amount > 1_000_000_000) return "Enter a positive quantity up to 1,000,000,000."
        if (unit.isBlank() || unit.length > 20) return "Enter a unit, such as kg or crates."
        val parsed = runCatching { LocalDate.parse(date) }.getOrNull() ?: return "Use a valid date in YYYY-MM-DD format."
        if (demand && parsed.isBefore(today)) return "The deadline cannot be in the past."
        val lat = latitude.toDoubleOrNull()
        val lon = longitude.toDoubleOrNull()
        if (lat == null || !lat.isFinite() || lat !in -90.0..90.0) return "Latitude must be between -90 and 90."
        if (lon == null || !lon.isFinite() || lon !in -180.0..180.0) return "Longitude must be between -180 and 180."
        return null
    }
}
