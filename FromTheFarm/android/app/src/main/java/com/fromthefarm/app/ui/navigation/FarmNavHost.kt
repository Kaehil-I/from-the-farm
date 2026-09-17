package com.fromthefarm.app.ui.navigation

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.fromthefarm.app.data.*
import com.fromthefarm.app.ui.FarmViewModel
import com.fromthefarm.app.ui.screens.RecordEditor
import com.fromthefarm.app.ui.screens.ProfileEditor
import com.fromthefarm.app.ui.screens.NearbyFilter
import com.fromthefarm.app.ui.screens.ListingPhoto
import com.fromthefarm.app.ui.screens.BiometricAction
import kotlin.math.roundToInt

/** Authenticated shell. The Part 1 sample screens are retained for design previews only. */
@Composable
fun FarmNavHost(vm: FarmViewModel = viewModel(factory = FarmViewModel.factory(LocalContext.current.applicationContext))) {
    val state by vm.state.collectAsStateWithLifecycle()
    val context = LocalContext.current
    var tab by rememberSaveable { mutableStateOf("Home") }
    var editor by rememberSaveable { mutableStateOf<String?>(null) }
    var editingId by rememberSaveable { mutableStateOf<String?>(null) }
    var matchId by rememberSaveable { mutableStateOf<String?>(null) }
    var deleteId by remember { mutableStateOf<String?>(null) }
    var completeId by remember { mutableStateOf<String?>(null) }
    var lastUserId by rememberSaveable { mutableStateOf<String?>(null) }
    val profile = state.profile
    if (state.biometricLocked) {
        Column(Modifier.fillMaxSize().padding(24.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
            Text("From The Farm is locked", style = MaterialTheme.typography.headlineSmall)
            Text("Unlock your saved Google session with your fingerprint or supported face recognition.")
            state.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            BiometricAction("Unlock with biometrics", !state.busy, vm::unlockWithBiometrics, vm::biometricError)
            TextButton(enabled = !state.busy, onClick = { vm.signIn(context) }) { Text("Use Google instead") }
            TextButton(enabled = !state.busy, onClick = vm::logout) { Text("Sign out") }
        }
        return
    }
    val onboarded = profile?.role in listOf("Farmer", "Buyer")
    val farmer = profile?.role == "Farmer"
    LaunchedEffect(profile?.userId) {
        if (profile != null && lastUserId != profile.userId) {
            tab = "Home"; editor = null; matchId = null; deleteId = null
            lastUserId = profile.userId
        }
    }
    LaunchedEffect(onboarded, state.loaded, state.busy, state.error) {
        if (onboarded && !state.loaded && !state.busy && state.error == null) vm.refresh()
    }
    LaunchedEffect(matchId, state.busy, state.detail, state.error) {
        if (onboarded && matchId != null && !state.busy && state.detail == null && state.error == null) {
            matchId?.let(vm::openMatch)
        }
    }
    BackHandler(editor != null || matchId != null) { if (!state.busy) { editor = null; matchId = null } }
    Scaffold(bottomBar = {
        if (onboarded && editor == null && matchId == null) NavigationBar {
            listOf("Home" to Icons.Default.Home, "Listings" to Icons.Default.List,
                "Calendar" to Icons.Default.CalendarToday, "Settings" to Icons.Default.Settings).forEach { (name, icon) ->
                NavigationBarItem(selected = tab == name, enabled = !state.busy, onClick = { tab = name },
                    icon = { Icon(icon, name) }, label = { Text(if (name == "Listings" && !farmer) "Demand" else name) })
            }
        }
    }) { padding ->
        Column(Modifier.fillMaxSize().padding(padding).padding(horizontal = 20.dp)
            .imePadding().verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Spacer(Modifier.height(12.dp))
            Text("From the farm", style = MaterialTheme.typography.headlineSmall)
            if (state.busy) LinearProgressIndicator(Modifier.fillMaxWidth())
            state.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            state.message?.let { Text(it, color = MaterialTheme.colorScheme.primary) }
            if (profile == null && state.firebaseSignedIn) {
                Text("Google sign-in succeeded.", style = MaterialTheme.typography.titleLarge)
                Text("Your farm profile could not be loaded yet. Retry when the service is available.")
                Button(enabled = !state.busy, onClick = vm::resumeSession) { Text("Retry loading profile") }
                if (!state.biometricEnabled) BiometricAction("Enable biometric unlock", !state.busy, vm::enableBiometrics, vm::biometricError)
                else {
                    TextButton(enabled = !state.busy, onClick = vm::lock) { Text("Lock app") }
                    TextButton(enabled = !state.busy, onClick = vm::disableBiometrics) { Text("Turn off biometric unlock") }
                }
                TextButton(enabled = !state.busy, onClick = vm::logout) { Text("Sign out") }
            } else if (profile == null) {
                Text("Connect. Grow. Sell.")
                Text("Sign in or create your account with Google to connect with local farmers and buyers.")
                Button(enabled = !state.busy, onClick = { vm.signIn(context) }) { Text("Continue with Google") }
                if (state.error != null) TextButton(enabled = !state.busy, onClick = vm::resumeSession) { Text("Retry saved session") }
            } else if (!onboarded) {
                ProfileEditor(profile, state.busy, true, vm::saveProfile)
                TextButton(enabled = !state.busy, onClick = vm::logout) { Text("Sign out") }
            } else if (editor != null) {
                val demand = editor == "demand"
                val missingRecord = editingId != null && if (demand) state.demands.none { it.id == editingId } else state.listings.none { it.id == editingId }
                if (missingRecord) {
                    Text("The record is not available. Return to your records and refresh.")
                    TextButton(enabled = !state.busy, onClick = { editor = null }) { Text("Back") }
                } else RecordEditor(demand, editingId, state.listings.find { it.id == editingId }, state.demands.find { it.id == editingId }, state.busy,
                    onBack = { editor = null }, onSave = { crop, amount, unit, date, location, photo ->
                        if (demand) vm.saveDemand(editingId, DemandWrite(crop, amount, unit, date, location)) { editor = null }
                        else vm.saveListing(editingId, ListingWrite(crop, amount, unit, date, location, photo)) { editor = null }
                    })
            } else if (matchId != null) {
                TextButton(enabled = !state.busy, onClick = { matchId = null }) { Text("Back to matches") }
                Text("Match details", style = MaterialTheme.typography.titleLarge)
                val detail = state.detail?.takeIf { it.matchId == matchId }
                state.matches.find { it.matchId == matchId }?.let { Text("${it.counterpart.cropType} · ${it.counterpart.quantity} ${it.counterpart.unit}") }
                if (detail != null) {
                    Text("${(detail.score * 100).roundToInt()}% fit · ${detail.status}")
                    if (detail.status in listOf("Confirmed", "Completed")) {
                        Text(detail.counterpartContact?.displayName ?: "Contact name unavailable")
                        Text(detail.counterpartContact?.phone ?: "No phone number has been shared.")
                    } else Text("Contact information is shared after confirmation.")
                    if (detail.status == "Suggested") Button(enabled = !state.busy,
                        onClick = { vm.confirm(detail.matchId) }) { Text("Confirm match") }
                    if (detail.status == "Confirmed") Button(enabled = !state.busy,
                        onClick = { completeId = detail.matchId }) { Text("Mark exchange completed") }
                    if (detail.status == "Completed" && detail.matchId !in state.ratedMatches) Row {
                        TextButton(enabled = !state.busy, onClick = { vm.rate(detail.matchId, true) }) { Text("Thumbs up") }
                        TextButton(enabled = !state.busy, onClick = { vm.rate(detail.matchId, false) }) { Text("Thumbs down") }
                    }
                }
                completeId?.takeIf { it == detail?.matchId && detail.status == "Confirmed" }?.let { id ->
                    AlertDialog(onDismissRequest = { completeId = null },
                        title = { Text("Complete this exchange?") },
                        text = { Text("Confirm that the produce exchange has taken place. You can rate it afterwards.") },
                        confirmButton = { TextButton(enabled = !state.busy, onClick = { completeId = null; vm.complete(id) }) { Text("Complete exchange") } },
                        dismissButton = { TextButton(onClick = { completeId = null }) { Text("Cancel") } })
                }
                if (matchId in state.ratedMatches) Text("You have rated this exchange.")
                TextButton(enabled = !state.busy, onClick = { matchId?.let(vm::openMatch) }) { Text("Refresh details") }
            } else {
                if (tab != "Settings") TextButton(enabled = !state.busy, onClick = vm::refresh) { Text("Refresh") }
                when (tab) {
                    "Home" -> {
                        Text("Your matches", style = MaterialTheme.typography.titleLarge)
                        Text("Ranked by crop, distance, quantity and harvest timing.")
                        if (state.loaded && state.error == null && state.matches.isEmpty()) Text("No matches yet. Add a listing or demand request, then refresh.")
                        state.matches.forEach { item ->
                            Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp)) {
                                Text(item.counterpart.cropType, style = MaterialTheme.typography.titleMedium)
                                Text("${item.counterpart.quantity} ${item.counterpart.unit} · ${"%.1f".format(item.counterpart.distanceKm)} km")
                                Text("${(item.score * 100).roundToInt()}% fit · ${item.status}")
                                Text("${if (farmer) "Needed by" else "Harvest"}: ${item.counterpart.relevantDate}")
                                TextButton(enabled = !state.busy, onClick = { matchId = item.matchId; vm.openMatch(item.matchId) }) { Text("View match") }
                            } }
                        }
                    }
                    "Listings" -> {
                        Text(if (farmer) "My listings" else "My demand requests", style = MaterialTheme.typography.titleLarge)
                        Button(enabled = !state.busy, onClick = { editingId = null; editor = if (farmer) "listing" else "demand" }) {
                            Text(if (farmer) "Add listing" else "Post demand")
                        }
                        if (farmer) {
                            if (state.loaded && state.error == null && state.listings.isEmpty()) Text("You have no active listings.")
                            state.listings.forEach { item ->
                                ListingPhoto(item.photoUrl)
                                RecordCard(item.cropType, "${item.quantity} ${item.unit} · ${item.harvestDate} · ${item.status}", state.busy,
                                    { editingId = item.id; editor = "listing" }, { deleteId = item.id })
                            }
                        } else {
                            if (state.loaded && state.error == null && state.demands.isEmpty()) Text("You have no open demand requests.")
                            state.demands.forEach { item -> RecordCard(item.cropType, "${item.quantityNeeded} ${item.unit} · ${item.deadline} · ${item.status}", state.busy,
                                { editingId = item.id; editor = "demand" }, { deleteId = item.id }) }
                            Text("Available produce", style = MaterialTheme.typography.titleLarge)
                            NearbyFilter(profile.searchRadiusKm, state.busy, vm::browse)
                            state.listings.forEach {
                                Text("${it.cropType} · ${it.quantity} ${it.unit} · ${it.harvestDate}")
                            }
                        }
                    }
                    "Calendar" -> {
                        Text("Harvest dates", style = MaterialTheme.typography.titleLarge)
                        if (state.loaded && state.error == null && state.listings.isEmpty()) Text("No harvests to show.")
                        state.listings.groupBy { it.harvestDate }.toSortedMap().forEach { (date, items) ->
                            Text(date, style = MaterialTheme.typography.titleMedium)
                            items.forEach { Text("${it.cropType} · ${it.quantity} ${it.unit}") }
                        }
                    }
                    "Settings" -> {
                        ProfileEditor(profile, state.busy, false, vm::saveProfile)
                        HorizontalDivider()
                        Text("Device security", style = MaterialTheme.typography.titleMedium)
                        if (!state.biometricEnabled) BiometricAction("Enable biometric unlock on this device", !state.busy, vm::enableBiometrics, vm::biometricError)
                        else {
                            TextButton(enabled = !state.busy, onClick = vm::lock) { Text("Lock app") }
                            TextButton(enabled = !state.busy, onClick = vm::disableBiometrics) { Text("Turn off biometric unlock") }
                        }
                        Text("Biometric unlock is enrolled on this device only, so it does not follow your account to another phone.",
                            style = MaterialTheme.typography.bodySmall)
                        HorizontalDivider()
                        TextButton(enabled = !state.busy, onClick = vm::logout) { Text("Sign out") }
                    }
                }
            }
            Spacer(Modifier.height(16.dp))
        }
    }
    deleteId?.let { id -> AlertDialog(onDismissRequest = { deleteId = null }, title = { Text("Remove this record?") },
        text = { Text("It will no longer appear in your active records.") },
        confirmButton = { TextButton(onClick = { deleteId = null; vm.delete(id, !farmer) }) { Text("Remove") } },
        dismissButton = { TextButton(onClick = { deleteId = null }) { Text("Cancel") } }) }
}

@Composable
private fun RecordCard(title: String, summary: String, busy: Boolean, edit: () -> Unit, remove: () -> Unit) {
    Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp)) {
        Text(title, style = MaterialTheme.typography.titleMedium)
        Text(summary)
        Row {
            TextButton(enabled = !busy, onClick = edit) { Text("Edit") }
            TextButton(enabled = !busy, onClick = remove) { Text("Remove") }
        }
    } }
}
