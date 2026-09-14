package com.fromthefarm.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.fromthefarm.app.data.CalendarEvent
import com.fromthefarm.app.data.SampleData
import com.fromthefarm.app.ui.theme.*

// day 0 = leading blank cell before the 1st.
private val LEADING_BLANKS = 2 // September 2026 mock starts on a Tuesday
private val DAYS_IN_MONTH = 30
private val SUPPLY_DAYS = setOf(3)
private val DEMAND_DAYS = setOf(15)

@Composable
fun HarvestCalendarScreen() {
    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Text("September", style = MaterialTheme.typography.titleMedium, color = FarmTextPrimary)
        Spacer(Modifier.height(12.dp))

        // Weekday header row
        Row(modifier = Modifier.fillMaxWidth()) {
            listOf("S", "M", "T", "W", "T", "F", "S").forEach { d ->
                Text(
                    d,
                    modifier = Modifier.weight(1f),
                    textAlign = TextAlign.Center,
                    style = MaterialTheme.typography.labelSmall,
                    color = FarmTextMuted
                )
            }
        }
        Spacer(Modifier.height(4.dp))

        // Month grid
        LazyVerticalGrid(
            columns = GridCells.Fixed(7),
            modifier = Modifier.height(220.dp)
        ) {
            items(LEADING_BLANKS) { Box(Modifier.aspectRatio(1f)) }
            items(DAYS_IN_MONTH) { index ->
                val day = index + 1
                DayCell(day)
            }
        }

        Spacer(Modifier.height(20.dp))
        Text("Upcoming", style = MaterialTheme.typography.bodySmall, color = FarmTextSecondary)
        Spacer(Modifier.height(8.dp))
        SampleData.calendarEvents.forEach { event -> CalendarEventRow(event) }
    }
}

@Composable
private fun DayCell(day: Int) {
    val bg = when (day) {
        in SUPPLY_DAYS -> FarmGreenContainer
        in DEMAND_DAYS -> FarmAmberContainer
        else -> Color.Transparent
    }
    val fg = when (day) {
        in SUPPLY_DAYS -> FarmGreenDark
        in DEMAND_DAYS -> FarmAmber
        else -> FarmTextPrimary
    }
    Box(
        modifier = Modifier
            .aspectRatio(1f)
            .padding(2.dp)
            .background(bg, CircleShape),
        contentAlignment = Alignment.Center
    ) {
        Text(day.toString(), style = MaterialTheme.typography.bodySmall, color = fg)
    }
}

@Composable
private fun CalendarEventRow(event: CalendarEvent) {
    val accent = if (event.isSupply) FarmGreen else FarmAmber
    Row(modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp)) {
        Box(
            modifier = Modifier
                .width(3.dp)
                .height(32.dp)
                .background(accent)
        )
        Spacer(Modifier.width(10.dp))
        Column {
            Text(event.date, style = MaterialTheme.typography.bodySmall, color = FarmTextSecondary)
            Text(event.label, style = MaterialTheme.typography.bodyMedium, color = FarmTextPrimary)
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun HarvestCalendarScreenPreview() {
    FromTheFarmTheme { HarvestCalendarScreen() }
}