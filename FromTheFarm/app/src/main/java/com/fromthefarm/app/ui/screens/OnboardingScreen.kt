package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Grass
import androidx.compose.material.icons.filled.ShoppingCart
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.fromthefarm.app.ui.theme.*
import androidx.compose.foundation.clickable
import com.fromthefarm.app.data.UserRole

@Composable
fun OnboardingScreen(onContinue: (UserRole) -> Unit = {}) {
    var role by remember { mutableStateOf(UserRole.FARMER) }
    var language by remember { mutableStateOf("English") }
    var radiusKm by remember { mutableStateOf(15f) }

    Column(modifier = Modifier.fillMaxSize().padding(20.dp)) {
        Text("Tell us about you", style = MaterialTheme.typography.titleMedium, color = FarmTextPrimary)
        Text("This helps us tailor your matches", style = MaterialTheme.typography.bodySmall, color = FarmTextSecondary)
        Spacer(Modifier.height(16.dp))

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            RoleCard(
                label = "Farmer",
                icon = Icons.Filled.Grass,
                selected = role == UserRole.FARMER,
                onClick = { role = UserRole.FARMER },
                modifier = Modifier.weight(1f)
            )
            RoleCard(
                label = "Buyer",
                icon = Icons.Filled.ShoppingCart,
                selected = role == UserRole.BUYER,
                onClick = { role = UserRole.BUYER },
                modifier = Modifier.weight(1f)
            )
        }

        Spacer(Modifier.height(20.dp))
        Text("Language", style = MaterialTheme.typography.bodySmall, color = FarmTextSecondary)
        Spacer(Modifier.height(6.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            listOf("English", "isiZulu", "Afrikaans").forEach { lang ->
                LanguagePill(lang, selected = language == lang, onClick = { language = lang })
            }
        }

        Spacer(Modifier.height(20.dp))
        Text("Search radius: ${radiusKm.toInt()} km", style = MaterialTheme.typography.bodySmall, color = FarmTextSecondary)
        Slider(
            value = radiusKm,
            onValueChange = { radiusKm = it },
            valueRange = 1f..50f,
            colors = SliderDefaults.colors(thumbColor = FarmGreen, activeTrackColor = FarmGreen)
        )

        Spacer(Modifier.weight(1f))
        Button(
            onClick = {onContinue(role)},
            modifier = Modifier.fillMaxWidth().height(46.dp),
            shape = RoundedCornerShape(10.dp),
            colors = ButtonDefaults.buttonColors(containerColor = FarmGreen)
        ) {
            Text("Continue")
        }
    }
}

@Composable
private fun RoleCard(
    label: String,
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    selected: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .background(if (selected) FarmGreenContainer else Color.White, RoundedCornerShape(12.dp))
            .border(
                width = if (selected) 1.5.dp else 0.5.dp,
                color = if (selected) FarmGreen else FarmBorder,
                shape = RoundedCornerShape(12.dp)
            )
            .clickableSimple(onClick)
            .padding(vertical = 14.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Icon(icon, contentDescription = null, tint = if (selected) FarmGreen else FarmTextSecondary)
        Spacer(Modifier.height(4.dp))
        Text(label, style = MaterialTheme.typography.bodyMedium, color = if (selected) FarmGreenDark else FarmTextPrimary)
    }
}

@Composable
private fun LanguagePill(label: String, selected: Boolean, onClick: () -> Unit) {
    Box(
        modifier = Modifier
            .background(if (selected) FarmGreen else Color.White, RoundedCornerShape(14.dp))
            .border(0.5.dp, if (selected) FarmGreen else FarmBorder, RoundedCornerShape(14.dp))
            .clickableSimple(onClick)
            .padding(horizontal = 12.dp, vertical = 6.dp)
    ) {
        Text(label, style = MaterialTheme.typography.bodySmall, color = if (selected) Color.White else FarmTextPrimary)
    }
}

private fun Modifier.clickableSimple(onClick: () -> Unit): Modifier =
    this.clickable(onClick = onClick)

@Preview(showBackground = true)
@Composable
private fun OnboardingScreenPreview() {
    FromTheFarmTheme { OnboardingScreen() }
}
