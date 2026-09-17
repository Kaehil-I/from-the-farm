package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.CalendarToday
import androidx.compose.material.icons.filled.LocationOn
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.fromthefarm.app.ui.theme.*

// Mirrors CreateListingScreen's structure so farmer and buyer forms feel consistent.
@Composable
fun CreateDemandScreen(onBack: () -> Unit = {}, onPost: () -> Unit = {}) {
    var cropType by remember { mutableStateOf("Spinach") }
    var quantity by remember { mutableStateOf("40") }
    var neededBy by remember { mutableStateOf("15 Sept 2026") }

    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack, modifier = Modifier.size(28.dp)) {
                Icon(Icons.Filled.ArrowBack, contentDescription = "Back")
            }
            Spacer(Modifier.width(4.dp))
            Text("New request", style = MaterialTheme.typography.titleMedium, color = FarmTextPrimary)
        }
        Spacer(Modifier.height(16.dp))

        FieldLabel("Crop type")
        OutlinedTextField(value = cropType, onValueChange = { cropType = it }, modifier = Modifier.fillMaxWidth(), singleLine = true)
        Spacer(Modifier.height(10.dp))

        FieldLabel("Quantity needed (kg)")
        OutlinedTextField(value = quantity, onValueChange = { quantity = it }, modifier = Modifier.fillMaxWidth(), singleLine = true)
        Spacer(Modifier.height(10.dp))

        FieldLabel("Needed by")
        OutlinedTextField(
            value = neededBy,
            onValueChange = { neededBy = it },
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

        Spacer(Modifier.weight(1f))
        Button(
            onClick = onPost,
            modifier = Modifier.fillMaxWidth().height(46.dp),
            shape = RoundedCornerShape(10.dp),
            colors = ButtonDefaults.buttonColors(containerColor = FarmGreen)
        ) {
            Text("Post request")
        }
    }
}

@Composable
private fun FieldLabel(text: String) {
    Text(text, style = MaterialTheme.typography.bodySmall, color = FarmTextSecondary, modifier = Modifier.padding(bottom = 4.dp))
}

@Preview(showBackground = true)
@Composable
private fun CreateDemandScreenPreview() {
    FromTheFarmTheme { CreateDemandScreen() }
}
