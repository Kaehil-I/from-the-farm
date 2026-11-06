package com.fromthefarm.app.notifications

import android.util.Log
import com.fromthefarm.app.data.FarmRepository
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

class FarmMessagingService : FirebaseMessagingService() {
    private companion object { const val TAG = "FarmMessaging" }
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    // Firebase rotates registration tokens (reinstall, restore, data clear). Tell
    // the API the new one if someone is signed in. If nobody is, nothing is lost:
    // the next sign-in registers whatever token is current.
    override fun onNewToken(token: String) {
        Log.i(TAG, "Registration token refreshed")
        val repository = FarmRepository(applicationContext)
        if (!repository.hasSession()) {
            Log.i(TAG, "No signed-in user; the next sign-in registers the new token")
            return
        }
        scope.launch {
            try {
                repository.registerDevice()
            } catch (e: Exception) {
                // Best effort. The app registers again on every launch.
                Log.w(TAG, "Device registration failed: ${e.javaClass.simpleName}")
            }
        }
    }

    override fun onMessageReceived(message: RemoteMessage) {
        Log.i(TAG, "Push received: type=${message.data[NotificationRules.EXTRA_TYPE]}")
        val title = message.notification?.title ?: return
        val body = message.notification?.body.orEmpty()
        MatchNotifications.show(
            applicationContext,
            title,
            body,
            message.data[NotificationRules.EXTRA_TYPE],
            message.data[NotificationRules.EXTRA_MATCH_ID]
        )
    }
}
