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
                    onError(if (code in listOf(BiometricPrompt.ERROR_USER_CANCELED, BiometricPrompt.ERROR_CANCELED, BiometricPrompt.ERROR_NEGATIVE_BUTTON))
                        "Biometric authentication cancelled." else "Biometric authentication unavailable. Try Google sign-in or check your device settings.")
                }
                override fun onAuthenticationFailed() { onError("Fingerprint or face not recognised. Try again.") }
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
            onError(when (available) {
                BiometricManager.BIOMETRIC_ERROR_NONE_ENROLLED ->
                    "Add a fingerprint or supported face unlock in Android Settings first."
                BiometricManager.BIOMETRIC_ERROR_NO_HARDWARE ->
                    "This device does not have supported biometric hardware. You can use Google sign-in."
                BiometricManager.BIOMETRIC_ERROR_HW_UNAVAILABLE ->
                    "Biometric hardware is temporarily unavailable. Try again or use Google sign-in."
                else -> "Biometric authentication is unavailable on this device. You can use Google sign-in."
            })
        } else {
            prompting = true
            prompt.authenticate(BiometricPrompt.PromptInfo.Builder()
                .setTitle("Unlock From The Farm")
                .setSubtitle("Verify your identity on this device")
                .setAllowedAuthenticators(authenticators)
                .setNegativeButtonText("Cancel").build())
        }
    }) { Text(label) }
}
