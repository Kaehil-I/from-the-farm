package com.fromthefarm.app.data

import android.content.Context
import androidx.credentials.CredentialManager
import androidx.credentials.ClearCredentialStateRequest
import androidx.credentials.CustomCredential
import androidx.credentials.GetCredentialRequest
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential
import com.google.firebase.FirebaseApp
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.GoogleAuthProvider
import com.fromthefarm.app.BuildConfig
import kotlinx.coroutines.tasks.await
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

interface FarmDataSource {
    val api: FarmApi
    fun hasSession(): Boolean
    suspend fun signIn(activity: Context)
    suspend fun token(): String
    suspend fun logout()
    fun biometricEnabled(): Boolean = false
    fun setBiometricEnabled(enabled: Boolean) {}
}

class FarmRepository(private val context: Context) : FarmDataSource {
    private val devicePreferences = context.getSharedPreferences("device_unlock", Context.MODE_PRIVATE)
    override fun biometricEnabled(): Boolean = auth.currentUser?.uid?.let { devicePreferences.getBoolean(it, false) } ?: false
    override fun setBiometricEnabled(enabled: Boolean) {
        val uid = auth.currentUser?.uid ?: error("Sign in with Google first.")
        devicePreferences.edit().putBoolean(uid, enabled).apply()
    }
    private val auth: FirebaseAuth get() {
        check(FirebaseApp.getApps(context).isNotEmpty()) { "Firebase is not configured. Add the team's google-services.json and rebuild." }
        return FirebaseAuth.getInstance()
    }
    override val api: FarmApi by lazy {
        check(BuildConfig.API_BASE_URL.startsWith("https://")) { "The farm service URL is not configured. Set API_BASE_URL in local.properties to the team's deployed HTTPS address, then rebuild." }
        Retrofit.Builder().baseUrl(BuildConfig.API_BASE_URL.trimEnd('/') + "/")
            .client(OkHttpClient.Builder().callTimeout(30, TimeUnit.SECONDS)
                .followRedirects(false).retryOnConnectionFailure(false).build())
            .addConverterFactory(GsonConverterFactory.create()).build().create(FarmApi::class.java)
    }
    override fun hasSession() = FirebaseApp.getApps(context).isNotEmpty() && auth.currentUser != null

    // Credential Manager obtains a Google credential; only the Firebase ID token goes to our API.
    // Reference: https://firebase.google.com/docs/auth/android/google-signin
    override suspend fun signIn(activity: Context) {
        val clientId = context.resources.getIdentifier("default_web_client_id", "string", context.packageName)
        check(clientId != 0) { "Firebase Google sign-in configuration is missing."
        }
        val option = GetSignInWithGoogleOption.Builder(context.getString(clientId)).build()
        val result = CredentialManager.create(activity).getCredential(activity,
            GetCredentialRequest.Builder().addCredentialOption(option).build()).credential
        check(result is CustomCredential && result.type == GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL) { "Google did not return a supported credential." }
        val google = GoogleIdTokenCredential.createFrom(result.data)
        auth.signInWithCredential(GoogleAuthProvider.getCredential(google.idToken, null)).await()
    }
    override suspend fun token(): String {
        val user = auth.currentUser ?: error("Please sign in again.")
        return "Bearer " + (user.getIdToken(false).await().token ?: error("Please sign in again."))
    }
    override suspend fun logout() {
        if (hasSession()) setBiometricEnabled(false)
        if (FirebaseApp.getApps(context).isNotEmpty()) auth.signOut()
        CredentialManager.create(context).clearCredentialState(ClearCredentialStateRequest())
    }
}
