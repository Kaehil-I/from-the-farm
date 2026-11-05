package com.fromthefarm.app.notifications

// Pure decisions about notifications, kept free of Android types so they run in
// plain JVM unit tests.
object NotificationRules {
    const val CHANNEL_ID = "matches"
    const val EXTRA_TYPE = "type"
    const val EXTRA_MATCH_ID = "matchId"

    // The "type" values the API puts in the push data payload.
    private val matchTypes = setOf("match_new", "match_confirmed", "match_completed")

    // The match a tapped notification should open, or null if the extras are not
    // a match notification. Extras can come from any app that fires an intent at
    // an exported activity, so the id is length-checked rather than trusted.
    fun matchIdFrom(type: String?, matchId: String?): String? {
        if (type !in matchTypes) return null
        val id = matchId?.trim() ?: return null
        return id.takeIf { it.isNotEmpty() && it.length <= 200 }
    }

    // Asking for POST_NOTIFICATIONS only exists from Android 13. It is requested
    // once, after onboarding and only if the user wants notifications, so the
    // prompt appears in a context where it makes sense; Android itself stops
    // showing the dialog after repeated refusals.
    fun shouldAskForPermission(
        sdkInt: Int,
        onboarded: Boolean,
        notificationsEnabled: Boolean,
        granted: Boolean,
        alreadyAsked: Boolean
    ): Boolean = sdkInt >= 33 && onboarded && notificationsEnabled && !granted && !alreadyAsked
}
