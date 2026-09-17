package com.fromthefarm.app.data

import org.junit.Assert.*
import org.junit.Test
import java.time.LocalDate

class FormValidationTest {
    private val today = LocalDate.of(2026, 9, 14)
    private fun validate(quantity: String = "12.5", date: String = "2026-09-15", latitude: String = "-29.85",
        longitude: String = "31.02", crop: String = "Tomatoes", unit: String = "kg", demand: Boolean = true) =
        FormValidation.validate(crop, quantity, unit, date, latitude, longitude, demand, today)

    @Test fun acceptsValidSouthAfricanCoordinatesAndDecimalQuantity() { assertNull(validate()) }
    @Test fun rejectsInvalidQuantities() {
        listOf("", "abc", "0", "-1", "NaN", "Infinity", "1e309", "1000000001").forEach { assertNotNull(it, validate(quantity = it)) }
    }
    @Test fun rejectsImpossibleDatesAndPastDeadlines() {
        listOf("", "2026-02-30", "14/09/2026", "2026-09-13").forEach { assertNotNull(it, validate(date = it)) }
        assertNull(validate(date = "2026-09-14"))
        assertNull(validate(date = "2026-09-13", demand = false))
    }
    @Test fun rejectsMissingOrOutOfRangeCoordinates() {
        listOf("", "NaN", "Infinity", "90.1", "-90.1").forEach { assertNotNull(validate(latitude = it)) }
        listOf("", "NaN", "180.1", "-180.1").forEach { assertNotNull(validate(longitude = it)) }
        assertNull(validate(latitude = "-90", longitude = "180"))
    }
    @Test fun requiresCropAndUnit() { assertNotNull(validate(crop = " ")); assertNotNull(validate(unit = "")) }
}
