package com.fromthefarm.app.notifications

import android.Manifest
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import com.fromthefarm.app.MainActivity
import com.fromthefarm.app.R

object MatchNotifications {
    // Android 8+ drops a notification posted to a channel that does not exist, and
    // the API names this same channel in the push it sends.
    fun ensureChannel(context: Context) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val channel = NotificationChannel(
            NotificationRules.CHANNEL_ID,
            context.getString(R.string.channel_matches_name),
            NotificationManager.IMPORTANCE_DEFAULT
        ).apply { description = context.getString(R.string.channel_matches_description) }
        context.getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    fun hasPermission(context: Context): Boolean =
        Build.VERSION.SDK_INT < 33 ||
            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED

    // Used when a push arrives while the app is open: Android only draws FCM
    // notifications itself when the app is in the background.
    fun show(context: Context, title: String, body: String, type: String?, matchId: String?) {
        if (!hasPermission(context)) return
        ensureChannel(context)
        val open = Intent(context, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
            putExtra(NotificationRules.EXTRA_TYPE, type)
            putExtra(NotificationRules.EXTRA_MATCH_ID, matchId)
        }
        val tap = PendingIntent.getActivity(
            context, matchId.hashCode(), open, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        val notification = NotificationCompat.Builder(context, NotificationRules.CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_notification)
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(NotificationCompat.BigTextStyle().bigText(body))
            .setContentIntent(tap)
            .setAutoCancel(true)
            .build()
        try {
            NotificationManagerCompat.from(context).notify(matchId.hashCode(), notification)
        } catch (_: SecurityException) {
            // The permission was revoked between the check above and posting.
        }
    }
}
