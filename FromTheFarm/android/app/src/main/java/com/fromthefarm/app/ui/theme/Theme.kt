package com.fromthefarm.app.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable

private val FarmColorScheme = lightColorScheme(
    primary = FarmGreen,
    onPrimary = FarmGreenContainer,
    primaryContainer = FarmGreenContainer,
    onPrimaryContainer = FarmGreenDark,
    secondary = FarmAmber,
    secondaryContainer = FarmAmberContainer,
    background = FarmBackground,
    surface = FarmSurface,
    onBackground = FarmTextPrimary,
    onSurface = FarmTextPrimary,
    outline = FarmBorder,
    error = FarmCoral,
    errorContainer = FarmCoralContainer
)

@Composable
fun FromTheFarmTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = FarmColorScheme,
        typography = FarmTypography,
        content = content
    )
}
