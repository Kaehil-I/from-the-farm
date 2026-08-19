package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.fromthefarm.app.data.Listing
import com.fromthefarm.app.data.SampleData
import com.fromthefarm.app.ui.theme.*

@Composable
fun HomeScreen(onOpenMatch: (String) -> Unit = {}) {
    Column(modifier = Modifier.fillMaxSize()) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text("From the farm", style = MaterialTheme.typography.titleMedium, color = FarmTextPrimary)
            Box {
                Icon(Icons.Filled.Notifications, contentDescription = "Notifications", tint = FarmTextSecondary)
                Box(
                    Modifier
                        .size(7.dp)
                        .align(Alignment.TopEnd)
                        .background(FarmCoral, CircleShape)
                )
            }
        }
        Text(
            "Your matches",
            style = MaterialTheme.typography.bodySmall,
            color = FarmTextSecondary,
            modifier = Modifier.padding(horizontal = 16.dp)
        )
        Spacer(Modifier.height(8.dp))
        LazyColumn(
            contentPadding = PaddingValues(horizontal = 16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(SampleData.nearbyMatches) { listing ->
                MatchCard(listing, onClick = { onOpenMatch(listing.id) })
            }
        }
    }
}

@Composable
private fun MatchCard(listing: Listing, onClick: () -> Unit) {
    Card(
        onClick = onClick,
        shape = RoundedCornerShape(10.dp),
        colors = CardDefaults.cardColors(containerColor = FarmSurface),
        border = androidx.compose.foundation.BorderStroke(0.5.dp, FarmBorder)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .background(FarmGreenContainer, RoundedCornerShape(8.dp))
            )
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text("${listing.cropName} · ${listing.quantityKg}kg", style = MaterialTheme.typography.bodyMedium, color = FarmTextPrimary)
                Text("${listing.distanceKm}km away", style = MaterialTheme.typography.bodySmall, color = FarmTextMuted)
            }
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun HomeScreenPreview() {
    FromTheFarmTheme { HomeScreen() }
}
