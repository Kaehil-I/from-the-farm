package com.fromthefarm.app.notifications

import org.junit.Assert.*
import org.junit.Test

class NotificationRulesTest {
    @Test fun everyMatchNotificationTypeOpensItsMatch() {
        listOf("match_new", "match_confirmed", "match_completed").forEach {
            assertEquals("listing-1:demand-1", NotificationRules.matchIdFrom(it, "listing-1:demand-1"))
        }
    }

    @Test fun anUnknownOrMissingTypeOpensNothing() {
        assertNull(NotificationRules.matchIdFrom(null, "listing-1:demand-1"))
        assertNull(NotificationRules.matchIdFrom("something_else", "listing-1:demand-1"))
    }

    @Test fun aMissingBlankOrOversizedMatchIdOpensNothing() {
        assertNull(NotificationRules.matchIdFrom("match_new", null))
        assertNull(NotificationRules.matchIdFrom("match_new", "   "))
        assertNull(NotificationRules.matchIdFrom("match_new", "x".repeat(201)))
    }

    @Test fun theMatchIdIsTrimmed() {
        assertEquals("a:b", NotificationRules.matchIdFrom("match_new", "  a:b "))
    }

    @Test fun permissionIsAskedOnlyOnAndroid13OrLater() {
        assertFalse(ask(sdk = 32))
        assertTrue(ask(sdk = 33))
        assertTrue(ask(sdk = 34))
    }

    @Test fun permissionIsNotAskedBeforeOnboarding() = assertFalse(ask(onboarded = false))

    @Test fun permissionIsNotAskedWhenNotificationsAreSwitchedOff() = assertFalse(ask(enabled = false))

    @Test fun permissionIsNotAskedWhenAlreadyGrantedOrAlreadyAsked() {
        assertFalse(ask(granted = true))
        assertFalse(ask(alreadyAsked = true))
    }

    private fun ask(
        sdk: Int = 34, onboarded: Boolean = true, enabled: Boolean = true,
        granted: Boolean = false, alreadyAsked: Boolean = false
    ) = NotificationRules.shouldAskForPermission(sdk, onboarded, enabled, granted, alreadyAsked)
}
