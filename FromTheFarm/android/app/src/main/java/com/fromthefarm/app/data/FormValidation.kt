package com.fromthefarm.app.data

import androidx.annotation.StringRes
import com.fromthefarm.app.R
import java.time.LocalDate

object FormValidation {
    /** Returns the string resource of the first problem found, or null when the form is acceptable. */
    @StringRes
    fun validate(crop: String, quantity: String, unit: String, date: String,
        latitude: String, longitude: String, demand: Boolean, today: LocalDate = LocalDate.now()): Int? {
        if (crop.isBlank() || crop.length > 100) return R.string.val_crop
        val amount = quantity.toDoubleOrNull()
        if (amount == null || !amount.isFinite() || amount <= 0 || amount > 1_000_000_000) return R.string.val_quantity
        if (unit.isBlank() || unit.length > 20) return R.string.val_unit
        val parsed = runCatching { LocalDate.parse(date) }.getOrNull() ?: return R.string.val_date
        if (demand && parsed.isBefore(today)) return R.string.val_deadline_past
        val lat = latitude.toDoubleOrNull()
        val lon = longitude.toDoubleOrNull()
        if (lat == null || !lat.isFinite() || lat !in -90.0..90.0) return R.string.val_latitude
        if (lon == null || !lon.isFinite() || lon !in -180.0..180.0) return R.string.val_longitude
        return null
    }
}
