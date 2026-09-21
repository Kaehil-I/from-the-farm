package com.fromthefarm.app.data

import org.junit.Assert.assertEquals
import org.junit.Test

class CropNamesTest {
    @Test fun normalizesCaseAndSpacing() {
        assertEquals("Cherry Tomatoes", CropNames.normalize("  cHERRY   TOMATOES "))
    }
}
