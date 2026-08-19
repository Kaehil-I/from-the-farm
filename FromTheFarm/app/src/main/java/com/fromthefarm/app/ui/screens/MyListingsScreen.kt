package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
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
fun MyListingsScreen(onAddListing: () -> Unit = {}, onOpenListing: (String) -> Unit = {}) {
    Column(modifier = Modifier.fillMaxSize()) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text("My listings", style = MaterialTheme.typography.titleMedium, color = FarmTextPrimary)
            Box(
                modifier = Modifier
                    .size(28.dp)
                    .background(FarmGreen, RoundedCornerShape(8.dp)),
                contentAlignment = Alignment.Center
            ) {
                IconButton(onClick = onAddListing) {
                    Icon(Icons.Filled.Add, contentDescription = "Add listing", tint = FarmGreenContainer)
                }
            }
        }
        LazyColumn(
            contentPadding = PaddingValues(horizontal = 16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(SampleData.myListings) { listing ->
                ListingRow(listing, onClick = { onOpenListing(listing.id) })
            }
        }
    }
}

@Composable
private fun ListingRow(listing: Listing, onClick: () -> Unit) {
    val (bg, fg) = when (listing.status) {
        "Matched" -> FarmGreenContainer to FarmGreenDark
        "Active" -> FarmAmberContainer to FarmAmber
        else -> FarmBorder to FarmTextSecondary
    }
    Card(
        onClick = onClick,
        shape = RoundedCornerShape(10.dp),
        colors = CardDefaults.cardColors(containerColor = FarmSurface),
        border = androidx.compose.foundation.BorderStroke(0.5.dp, FarmBorder)
    ) {
        Column(Modifier.padding(10.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("${listing.cropName} · ${listing.quantityKg}kg", style = MaterialTheme.typography.bodyMedium, color = FarmTextPrimary)
                Box(
                    modifier = Modifier.background(bg, RoundedCornerShape(8.dp)).padding(horizontal = 8.dp, vertical = 2.dp)
                ) {
                    Text(listing.status, style = MaterialTheme.typography.labelSmall, color = fg)
                }
            }
            Text("Harvest ${listing.harvestDate}", style = MaterialTheme.typography.bodySmall, color = FarmTextMuted)
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun MyListingsScreenPreview() {
    FromTheFarmTheme { MyListingsScreen() }
}
