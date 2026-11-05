package com.fromthefarm.app.ui

import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import java.util.Locale

/** The profile's preferred language is the single setting; the app locale follows it. */
object AppLanguage {
    val supported = setOf("en", "zu", "af")

    fun apply(code: String) {
        if (code !in supported) return
        val current = AppCompatDelegate.getApplicationLocales().get(0)?.language ?: Locale.getDefault().language
        if (current != code) AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags(code))
    }
}
