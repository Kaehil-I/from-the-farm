package com.fromthefarm.app.notifications

import com.fromthefarm.app.data.FarmRepository
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

class FarmMessagingService : FirebaseMessagingService() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    // Firebase rotates registration tokens (reinstall, restore, data clear). Tell
    // the API the new one if someone is signed in. If nobody is, nothing is lost:
    // the next sign-in registers whatever token is current.
    override fun onNewToken(token: String) {
        val repository = FarmRepository(applicationContext)
        if (!repository.hasSession()) return
        scope.launch {
            try {
                repository.registerDevice()
            } catch (_: Exception) {
                // Best effort. The app registers again on every launch.
            }
        }
    }

    override fun onMessageReceived(message: RemoteMessage) {
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
