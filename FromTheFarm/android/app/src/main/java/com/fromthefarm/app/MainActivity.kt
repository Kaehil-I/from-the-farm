package com.fromthefarm.app

import android.os.Bundle
import androidx.fragment.app.FragmentActivity
import androidx.activity.viewModels
import com.fromthefarm.app.ui.FarmViewModel
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import com.fromthefarm.app.ui.navigation.FarmNavHost
import com.fromthefarm.app.ui.theme.FromTheFarmTheme

class MainActivity : FragmentActivity() {
    private val farmViewModel: FarmViewModel by viewModels { FarmViewModel.factory(applicationContext) }
    override fun onStop() {
        farmViewModel.lock()
        super.onStop()
    }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            FromTheFarmTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    FarmNavHost(farmViewModel)
                }
            }
        }
    }
}
