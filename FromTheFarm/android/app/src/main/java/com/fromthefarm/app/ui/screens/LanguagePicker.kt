package com.fromthefarm.app.ui.screens

import androidx.appcompat.app.AppCompatDelegate
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.core.os.LocaleListCompat
import com.fromthefarm.app.R

/** Language names are shown in their own language so they stay readable after a wrong switch. */
private val languages = listOf("en" to "English", "zu" to "isiZulu", "af" to "Afrikaans")

@Composable
fun LanguagePicker() {
    val current = AppCompatDelegate.getApplicationLocales().get(0)?.language
        ?.takeIf { code -> languages.any { it.first == code } } ?: "en"
    Text(stringResource(R.string.language_title), style = MaterialTheme.typography.titleMedium)
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
        languages.forEach { (code, name) ->
            FilterChip(
                selected = code == current,
                onClick = { AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags(code)) },
                label = { Text(name) }
            )
        }
    }
}
