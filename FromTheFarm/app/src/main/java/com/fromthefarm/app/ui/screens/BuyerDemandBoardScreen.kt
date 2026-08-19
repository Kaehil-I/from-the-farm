package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.fromthefarm.app.data.DemandRequest
import com.fromthefarm.app.data.SampleData
import com.fromthefarm.app.ui.theme.*

@Composable
fun BuyerDemandBoardScreen(onAddRequest: () -> Unit = {}) {
    var showingNearby by remember { mutableStateOf(true) }

    Column(modifier = Modifier.fillMaxSize()) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text("Demand board", style = MaterialTheme.typography.titleMedium, color = FarmTextPrimary)
            Box(
                modifier = Modifier.size(28.dp).background(FarmGreen, RoundedCornerShape(8.dp)),
                contentAlignment = Alignment.Center
            ) {
                IconButton(onClick = onAddRequest) {
                    Icon(Icons.Filled.Add, contentDescription = "Post request", tint = FarmGreenContainer)
                }
            }
        }
        Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            FilterPill("Nearby", selected = showingNearby) { showingNearby = true }
            FilterPill("My requests", selected = !showingNearby) { showingNearby = false }
        }
        Spacer(Modifier.height(8.dp))
        LazyColumn(
            contentPadding = PaddingValues(horizontal = 16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(SampleData.demandBoard) { request -> DemandCard(request) }
        }
    }
}

@Composable
private fun FilterPill(label: String, selected: Boolean, onClick: () -> Unit) {
    Box(
        modifier = Modifier
            .background(if (selected) FarmGreen else Color.White, RoundedCornerShape(14.dp))
            .border(0.5.dp, if (selected) FarmGreen else FarmBorder, RoundedCornerShape(14.dp))
            .clickable(onClick = onClick)
            .padding(horizontal = 12.dp, vertical = 6.dp)
    ) {
        Text(
            label,
            style = MaterialTheme.typography.labelSmall,
            color = if (selected) Color.White else FarmTextPrimary
        )
    }
}

@Composable
private fun DemandCard(request: DemandRequest) {
    Card(
        shape = RoundedCornerShape(10.dp),
        colors = CardDefaults.cardColors(containerColor = FarmSurface),
        border = androidx.compose.foundation.BorderStroke(0.5.dp, FarmBorder)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(modifier = Modifier.size(34.dp).background(FarmAmberContainer, RoundedCornerShape(8.dp)))
            Spacer(Modifier.width(10.dp))
            Column(Modifier.weight(1f)) {
                Text("${request.cropName} · ${request.quantityKg}kg", style = MaterialTheme.typography.bodyMedium, color = FarmTextPrimary)
                Text("${request.postedByFarmName}, ${request.distanceKm}km", style = MaterialTheme.typography.bodySmall, color = FarmTextMuted)
            }
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun BuyerDemandBoardScreenPreview() {
    FromTheFarmTheme { BuyerDemandBoardScreen() }
}
