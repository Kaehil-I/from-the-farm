package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material.icons.filled.Phone
import androidx.compose.material.icons.filled.ThumbDown
import androidx.compose.material.icons.filled.ThumbUp
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.fromthefarm.app.ui.theme.*

@Composable
fun MatchDetailScreen(
    onBack: () -> Unit = {},
    onRateUp: () -> Unit = {},
    onRateDown: () -> Unit = {}
) {
    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack, modifier = Modifier.size(28.dp)) {
                Icon(Icons.Filled.ArrowBack, contentDescription = "Back")
            }
            Spacer(Modifier.width(4.dp))
            Text("Match detail", style = MaterialTheme.typography.titleMedium, color = FarmTextPrimary)
        }
        Spacer(Modifier.height(16.dp))

        Card(
            shape = RoundedCornerShape(10.dp),
            colors = CardDefaults.cardColors(containerColor = FarmSurface),
            border = androidx.compose.foundation.BorderStroke(0.5.dp, FarmBorder)
        ) {
            Column(Modifier.padding(12.dp)) {
                Text("Tomatoes · 50kg", style = MaterialTheme.typography.bodyMedium, color = FarmTextPrimary)
                Text("Matched with Thandiwe M.", style = MaterialTheme.typography.bodySmall, color = FarmTextMuted)
            }
        }
        Spacer(Modifier.height(12.dp))

        InfoRow(Icons.Filled.Phone, "071 234 5678")
        InfoRow(Icons.Filled.LocationOn, "2.3km away, Umlazi")

        Spacer(Modifier.height(24.dp))
        Text(
            "How did the exchange go?",
            style = MaterialTheme.typography.bodySmall,
            color = FarmTextSecondary,
            modifier = Modifier.fillMaxWidth(),
            textAlign = androidx.compose.ui.text.style.TextAlign.Center
        )
        Spacer(Modifier.height(8.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.Center
        ) {
            RatingButton(Icons.Filled.ThumbUp, onRateUp, tint = FarmGreen)
            Spacer(Modifier.width(12.dp))
            RatingButton(Icons.Filled.ThumbDown, onRateDown, tint = FarmTextMuted)
        }
    }
}

@Composable
private fun InfoRow(icon: androidx.compose.ui.graphics.vector.ImageVector, text: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(icon, contentDescription = null, tint = FarmTextSecondary, modifier = Modifier.size(16.dp))
        Spacer(Modifier.width(8.dp))
        Text(text, style = MaterialTheme.typography.bodyMedium, color = FarmTextPrimary)
    }
}

@Composable
private fun RatingButton(icon: androidx.compose.ui.graphics.vector.ImageVector, onClick: () -> Unit, tint: androidx.compose.ui.graphics.Color) {
    Box(
        modifier = Modifier
            .size(44.dp)
            .border(0.5.dp, FarmBorder, CircleShape),
        contentAlignment = Alignment.Center
    ) {
        IconButton(onClick = onClick) {
            Icon(icon, contentDescription = null, tint = tint)
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun MatchDetailScreenPreview() {
    FromTheFarmTheme { MatchDetailScreen() }
}
