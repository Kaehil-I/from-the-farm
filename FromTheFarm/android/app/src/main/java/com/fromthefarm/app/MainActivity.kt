package com.fromthefarm.app

import android.Manifest
import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.util.Log
import androidx.activity.result.contract.ActivityResultContracts
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import com.fromthefarm.app.notifications.MatchNotifications
import com.fromthefarm.app.notifications.NotificationRules
import kotlinx.coroutines.launch
import androidx.appcompat.app.AppCompatActivity
import androidx.activity.viewModels
import com.fromthefarm.app.ui.FarmViewModel
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import com.fromthefarm.app.ui.navigation.FarmNavHost
import com.fromthefarm.app.ui.theme.FromTheFarmTheme

class MainActivity : AppCompatActivity() {
    private companion object { const val TAG = "MainActivity" }
    private val farmViewModel: FarmViewModel by viewModels { FarmViewModel.factory(applicationContext) }
    private val notificationPermission = registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        Log.i(TAG, "Notification permission granted: $granted")
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        Log.i(TAG, "onNewIntent: app brought forward by a new intent")
        openMatchFrom(intent)
    }
    private fun openMatchFrom(intent: Intent?) {
        farmViewModel.openMatchFromNotification(NotificationRules.matchIdFrom(
            intent?.getStringExtra(NotificationRules.EXTRA_TYPE), intent?.getStringExtra(NotificationRules.EXTRA_MATCH_ID)))
    }
    // Asked once, after onboarding, and only for people who have notifications switched on.
    private fun askForNotificationsIfNeeded() {
        val profile = farmViewModel.state.value.profile
        val preferences = getSharedPreferences("notification_prompt", MODE_PRIVATE)
        if (NotificationRules.shouldAskForPermission(Build.VERSION.SDK_INT, profile?.role != null,
                profile?.notificationsEnabled == true, MatchNotifications.hasPermission(this),
                preferences.getBoolean("asked", false))) {
            preferences.edit().putBoolean("asked", true).apply()
            if (Build.VERSION.SDK_INT >= 33) notificationPermission.launch(Manifest.permission.POST_NOTIFICATIONS)
        }
    }
    override fun onStart() {
        super.onStart()
        Log.i(TAG, "onStart")
    }
    override fun onResume() {
        super.onResume()
        Log.i(TAG, "onResume")
    }
    override fun onPause() {
        Log.i(TAG, "onPause")
        super.onPause()
    }
    override fun onStop() {
        Log.i(TAG, "onStop: locking the app if the biometric lock is on")
        farmViewModel.lock()
        super.onStop()
    }
    override fun onDestroy() {
        Log.i(TAG, "onDestroy (configuration change: $isChangingConfigurations)")
        super.onDestroy()
    }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        Log.i(TAG, "onCreate (fresh launch: ${savedInstanceState == null})")
        MatchNotifications.ensureChannel(this)
        // A rotation recreates the activity with the same intent; only act on a fresh launch.
        if (savedInstanceState == null) openMatchFrom(intent)
        lifecycleScope.launch {
            repeatOnLifecycle(Lifecycle.State.STARTED) {
                farmViewModel.state.collect { askForNotificationsIfNeeded() }
            }
        }
        setContent {
            FromTheFarmTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    FarmNavHost(farmViewModel)
                }
            }
        }
    }
}
