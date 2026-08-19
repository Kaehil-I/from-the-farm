package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.fromthefarm.app.ui.theme.*

@Composable
fun SettingsScreen(onLogout: () -> Unit = {}) {
    var matchAlerts by remember { mutableStateOf(true) }
    var biometricLock by remember { mutableStateOf(true) }

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Text("Settings", style = MaterialTheme.typography.titleMedium, color = FarmTextPrimary)
        Spacer(Modifier.height(12.dp))

        SettingsRow("Language") { Text("English", style = MaterialTheme.typography.bodySmall, color = FarmTextMuted) }
        SettingsRow("Search radius") { Text("15 km", style = MaterialTheme.typography.bodySmall, color = FarmTextMuted) }
        SettingsRow("New match alerts") {
            Switch(
                checked = matchAlerts,
                onCheckedChange = { matchAlerts = it },
                colors = SwitchDefaults.colors(checkedTrackColor = FarmGreen)
            )
        }
        SettingsRow("Biometric lock") {
            Switch(
                checked = biometricLock,
                onCheckedChange = { biometricLock = it },
                colors = SwitchDefaults.colors(checkedTrackColor = FarmGreen)
            )
        }
        SettingsRow("Log out", labelColor = FarmCoral) {
            TextButton(onClick = onLogout) { Text("Log out", color = FarmCoral) }
        }
    }
}

@Composable
private fun SettingsRow(label: String, labelColor: androidx.compose.ui.graphics.Color = FarmTextPrimary, trailing: @Composable () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(label, style = MaterialTheme.typography.bodyMedium, color = labelColor)
        trailing()
    }
    Divider(color = FarmBorder, thickness = 0.5.dp)
}

@Preview(showBackground = true)
@Composable
private fun SettingsScreenPreview() {
    FromTheFarmTheme { SettingsScreen() }
}
