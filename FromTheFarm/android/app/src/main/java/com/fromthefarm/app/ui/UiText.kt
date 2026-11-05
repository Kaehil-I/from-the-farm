package com.fromthefarm.app.ui

import androidx.annotation.StringRes
import androidx.compose.runtime.Composable
import androidx.compose.ui.res.stringResource

/**
 * User-facing text the ViewModel can describe without knowing the app language: the screen turns
 * it into a string, so a message follows the language chosen when it is shown.
 */
sealed interface UiText {
    data class Res(@StringRes val id: Int, val args: List<Any> = emptyList()) : UiText
    /** Text that is already final, such as a message from the system or a developer-only setup error. */
    data class Raw(val text: String) : UiText
    data class Joined(val parts: List<UiText>, val separator: String = ", ") : UiText
}

@Composable
fun UiText.asString(): String = when (this) {
    is UiText.Raw -> text
    is UiText.Joined -> parts.map { it.asString() }.joinToString(separator)
    is UiText.Res -> stringResource(id, *args.map { if (it is UiText) it.asString() else it }.toTypedArray())
}

/** An error whose message the person should read, thrown from places that cannot reach resources. */
class UserFacingException(val text: UiText) : IllegalStateException()
