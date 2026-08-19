package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.fromthefarm.app.ui.theme.*

// Field set mirrors the API's listing schema — keep in sync with Kaehil's endpoint definitions.
@Composable
fun CreateListingScreen(onBack: () -> Unit = {}, onSave: () -> Unit = {}) {
    var cropType by remember { mutableStateOf("Tomatoes") }
    var quantity by remember { mutableStateOf("50") }
    var unit by remember { mutableStateOf("kg") }
    var harvestDate by remember { mutableStateOf("3 Sept 2026") }

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack, modifier = Modifier.size(28.dp)) {
                Icon(Icons.Filled.ArrowBack, contentDescription = "Back")
            }
            Spacer(Modifier.width(4.dp))
            Text("New listing", style = MaterialTheme.typography.titleMedium, color = FarmTextPrimary)
        }
        Spacer(Modifier.height(16.dp))

        FieldLabel("Crop type")
        OutlinedTextField(
            value = cropType,
            onValueChange = { cropType = it },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true
        )
        Spacer(Modifier.height(10.dp))

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Column(Modifier.weight(1f)) {
                FieldLabel("Quantity")
                OutlinedTextField(value = quantity, onValueChange = { quantity = it }, singleLine = true)
            }
            Column(Modifier.weight(1f)) {
                FieldLabel("Unit")
                OutlinedTextField(value = unit, onValueChange = { unit = it }, singleLine = true)
            }
        }
        Spacer(Modifier.height(10.dp))

        FieldLabel("Harvest date")
        OutlinedTextField(
            value = harvestDate,
            onValueChange = { harvestDate = it },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            trailingIcon = { Icon(Icons.Filled.CalendarToday, contentDescription = null) }
        )
        Spacer(Modifier.height(10.dp))

        FieldLabel("Location")
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .border(0.5.dp, FarmBorder, RoundedCornerShape(8.dp))
                .padding(horizontal = 12.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text("Use current location", style = MaterialTheme.typography.bodyMedium, color = FarmTextPrimary)
            Icon(Icons.Filled.LocationOn, contentDescription = null, tint = FarmTextMuted)
        }
        Spacer(Modifier.height(10.dp))

        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(64.dp)
                .border(0.5.dp, FarmBorder, RoundedCornerShape(8.dp)),
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(Icons.Filled.CameraAlt, contentDescription = null, tint = FarmTextMuted)
                Text("Add photo", style = MaterialTheme.typography.bodySmall, color = FarmTextMuted)
            }
        }

        Spacer(Modifier.weight(1f))
        Button(
            onClick = onSave,
            modifier = Modifier.fillMaxWidth().height(46.dp),
            shape = RoundedCornerShape(10.dp),
            colors = ButtonDefaults.buttonColors(containerColor = FarmGreen)
        ) {
            Text("Save listing")
        }
    }
}

@Composable
private fun FieldLabel(text: String) {
    Text(text, style = MaterialTheme.typography.bodySmall, color = FarmTextSecondary, modifier = Modifier.padding(bottom = 4.dp))
}

@Preview(showBackground = true)
@Composable
private fun CreateListingScreenPreview() {
    FromTheFarmTheme { CreateListingScreen() }
}
