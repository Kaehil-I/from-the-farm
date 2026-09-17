package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Fingerprint
import androidx.compose.material.icons.filled.Spa
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.fromthefarm.app.ui.theme.*

@Composable
fun LoginScreen(
    onContinueWithGoogle: () -> Unit = {},
    onUseBiometrics: () -> Unit = {}
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 24.dp)
            .padding(top = 64.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Box(
            modifier = Modifier
                .size(64.dp)
                .background(FarmGreen, RoundedCornerShape(18.dp)),
            contentAlignment = Alignment.Center
        ) {
            Icon(Icons.Filled.Spa, contentDescription = null, tint = FarmGreenContainer)
        }
        Spacer(Modifier.height(16.dp))
        Text("From the farm", style = MaterialTheme.typography.titleLarge, color = FarmTextPrimary)
        Text("Connect. Grow. Sell.", style = MaterialTheme.typography.bodySmall, color = FarmTextSecondary)
        Spacer(Modifier.height(48.dp))

        OutlinedButton(
            onClick = onContinueWithGoogle,
            modifier = Modifier.fillMaxWidth().height(46.dp),
            shape = RoundedCornerShape(10.dp)
        ) {
            Text("Continue with Google")
        }
        Spacer(Modifier.height(10.dp))
        OutlinedButton(
            onClick = onUseBiometrics,
            modifier = Modifier.fillMaxWidth().height(46.dp),
            shape = RoundedCornerShape(10.dp)
        ) {
            Icon(Icons.Filled.Fingerprint, contentDescription = null, modifier = Modifier.size(18.dp))
            Spacer(Modifier.width(8.dp))
            Text("Use biometrics")
        }
        Spacer(Modifier.weight(1f))
        Text(
            "By continuing you agree to our terms",
            style = MaterialTheme.typography.bodySmall,
            color = FarmTextMuted,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(bottom = 24.dp)
        )
    }
}

@Preview(showBackground = true)
@Composable
private fun LoginScreenPreview() {
    FromTheFarmTheme { LoginScreen() }
}
