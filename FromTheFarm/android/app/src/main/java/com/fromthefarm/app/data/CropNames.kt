package com.fromthefarm.app.data

import java.util.Locale

object CropNames {
    fun normalize(value: String): String = value.trim().lowercase(Locale.ROOT).split(Regex("\\s+"))
        .joinToString(" ") { word -> word.replaceFirstChar { it.titlecase(Locale.ROOT) } }
}
