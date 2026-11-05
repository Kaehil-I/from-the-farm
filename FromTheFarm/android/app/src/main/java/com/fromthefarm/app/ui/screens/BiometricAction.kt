package com.fromthefarm.app.ui.screens

import android.content.Context
import android.content.ContextWrapper
import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalContext
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity
import com.fromthefarm.app.R

private fun Context.hostActivity(): FragmentActivity? = when (this) {
    is FragmentActivity -> this
    is ContextWrapper -> baseContext.hostActivity()
    else -> null
}

/** A local app-access check. Firebase continues to own the saved remote session. */
@Composable
fun BiometricAction(label: String, enabled: Boolean, success: () -> Unit, error: (String) -> Unit) {
    val context = LocalContext.current
    val activity = context.hostActivity()
    val onSuccess by rememberUpdatedState(success)
    val onError by rememberUpdatedState(error)
    var prompting by remember { mutableStateOf(false) }
    val prompt = remember(activity) {
        activity?.let { host ->
            BiometricPrompt(host, ContextCompat.getMainExecutor(host), object : BiometricPrompt.AuthenticationCallback() {
                override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) {
                    prompting = false
                    onSuccess()
                }
                override fun onAuthenticationError(code: Int, message: CharSequence) {
                    prompting = false
                    onError(host.getString(if (code in listOf(BiometricPrompt.ERROR_USER_CANCELED, BiometricPrompt.ERROR_CANCELED, BiometricPrompt.ERROR_NEGATIVE_BUTTON))
                        R.string.biometric_cancelled else R.string.biometric_unavailable_try_google))
                }
                override fun onAuthenticationFailed() { onError(host.getString(R.string.biometric_not_recognised)) }
            })
        }
    }
    DisposableEffect(prompt) { onDispose { if (prompting) prompt?.cancelAuthentication() } }
    Button(enabled = enabled && !prompting, onClick = {
        // Class 2+ accepts Android-supported fingerprint and face authentication.
        // Requiring BIOMETRIC_STRONG alone excludes face unlock on many valid devices.
        val authenticators = BiometricManager.Authenticators.BIOMETRIC_WEAK
        val available = BiometricManager.from(context).canAuthenticate(authenticators)
        if (available != BiometricManager.BIOMETRIC_SUCCESS || prompt == null) {
            onError(context.getString(when (available) {
                BiometricManager.BIOMETRIC_ERROR_NONE_ENROLLED -> R.string.biometric_none_enrolled
                BiometricManager.BIOMETRIC_ERROR_NO_HARDWARE -> R.string.biometric_no_hardware
                BiometricManager.BIOMETRIC_ERROR_HW_UNAVAILABLE -> R.string.biometric_hw_unavailable
                else -> R.string.biometric_unavailable_device
            }))
        } else {
            prompting = true
            prompt.authenticate(BiometricPrompt.PromptInfo.Builder()
                .setTitle(context.getString(R.string.biometric_prompt_title))
                .setSubtitle(context.getString(R.string.biometric_prompt_subtitle))
                .setAllowedAuthenticators(authenticators)
                .setNegativeButtonText(context.getString(R.string.cancel)).build())
        }
    }) { Text(label) }
}
