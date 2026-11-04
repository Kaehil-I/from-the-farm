package com.fromthefarm.app.ui.navigation

import androidx.activity.compose.BackHandler
import androidx.annotation.StringRes
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
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.fromthefarm.app.R
import com.fromthefarm.app.data.*
import com.fromthefarm.app.ui.FarmViewModel
import com.fromthefarm.app.ui.screens.RecordEditor
import com.fromthefarm.app.ui.screens.ProfileEditor
import com.fromthefarm.app.ui.screens.NearbyFilter
import com.fromthefarm.app.ui.screens.ListingPhoto
import com.fromthefarm.app.ui.screens.BiometricAction
import com.fromthefarm.app.ui.screens.LanguagePicker
import kotlin.math.roundToInt
import java.time.Instant
import java.time.ZoneOffset

/** Tab ids are stored in saved state, so they stay fixed while the label is looked up per language. */
private data class NavTab(val id: String, val icon: androidx.compose.ui.graphics.vector.ImageVector, @StringRes val label: Int)

private val tabs = listOf(
    NavTab("Home", Icons.Default.Home, R.string.tab_home),
    NavTab("Listings", Icons.Default.List, R.string.tab_listings),
    NavTab("Calendar", Icons.Default.CalendarToday, R.string.tab_calendar),
    NavTab("Settings", Icons.Default.Settings, R.string.tab_settings),
)

/** Status values come from the API in English; unknown values are shown as received. */
@Composable
private fun statusLabel(status: String): String = when (status) {
    "Suggested" -> stringResource(R.string.status_suggested)
    "Confirmed" -> stringResource(R.string.status_confirmed)
    "Completed" -> stringResource(R.string.status_completed)
    "Matched" -> stringResource(R.string.status_matched)
    "Open" -> stringResource(R.string.status_open)
    "Active" -> stringResource(R.string.status_active)
    "Expired" -> stringResource(R.string.status_expired)
    "Deleted" -> stringResource(R.string.status_deleted)
    else -> status
}

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
    var lastRole by rememberSaveable { mutableStateOf<String?>(null) }
    var demandSeedCrop by rememberSaveable { mutableStateOf<String?>(null) }
    val profile = state.profile
    if (state.biometricLocked) {
        Column(Modifier.fillMaxSize().padding(24.dp), verticalArrangement = Arrangement.spacedBy(16.dp)) {
            Text(stringResource(R.string.locked_title), style = MaterialTheme.typography.headlineSmall)
            Text(stringResource(R.string.locked_body))
            state.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            BiometricAction(stringResource(R.string.unlock_biometrics), !state.busy, vm::unlockWithBiometrics, vm::biometricError)
            TextButton(enabled = !state.busy, onClick = { vm.signIn(context) }) { Text(stringResource(R.string.use_google_instead)) }
            TextButton(enabled = !state.busy, onClick = vm::logout) { Text(stringResource(R.string.sign_out)) }
        }
        return
    }
    val onboarded = profile?.role in listOf("Farmer", "Buyer")
    val farmer = profile?.role == "Farmer"
    LaunchedEffect(profile?.userId) {
        if (profile != null && lastUserId != profile.userId) {
            tab = "Home"; editor = null; matchId = null; deleteId = null; demandSeedCrop = null
            lastUserId = profile.userId
            lastRole = profile.role
        }
    }
    LaunchedEffect(profile?.role) {
        if (profile != null && lastRole != null && lastRole != profile.role) {
            tab = "Home"; editor = null; editingId = null; matchId = null; deleteId = null; completeId = null; demandSeedCrop = null
        }
        if (profile != null) lastRole = profile.role
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
            tabs.forEach { item ->
                val label = stringResource(if (item.id == "Listings" && !farmer) R.string.tab_demand else item.label)
                NavigationBarItem(selected = tab == item.id, enabled = !state.busy, onClick = { tab = item.id },
                    icon = { Icon(item.icon, label) }, label = { Text(label) })
            }
        }
    }) { padding ->
        Column(Modifier.fillMaxSize().padding(padding).padding(horizontal = 20.dp)
            .imePadding().verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Spacer(Modifier.height(12.dp))
            Text(stringResource(R.string.app_heading), style = MaterialTheme.typography.headlineSmall)
            if (state.busy) LinearProgressIndicator(Modifier.fillMaxWidth())
            state.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            state.message?.let { Text(it, color = MaterialTheme.colorScheme.primary) }
            if (profile == null && state.firebaseSignedIn) {
                Text(stringResource(R.string.google_signin_succeeded), style = MaterialTheme.typography.titleLarge)
                Text(stringResource(R.string.profile_load_failed))
                Button(enabled = !state.busy, onClick = vm::resumeSession) { Text(stringResource(R.string.retry_profile)) }
                if (!state.biometricEnabled) BiometricAction(stringResource(R.string.enable_biometric_unlock), !state.busy, vm::enableBiometrics, vm::biometricError)
                else {
                    TextButton(enabled = !state.busy, onClick = vm::lock) { Text(stringResource(R.string.lock_app)) }
                    TextButton(enabled = !state.busy, onClick = vm::disableBiometrics) { Text(stringResource(R.string.turn_off_biometric)) }
                }
                TextButton(enabled = !state.busy, onClick = vm::logout) { Text(stringResource(R.string.sign_out)) }
            } else if (profile == null) {
                Text(stringResource(R.string.tagline))
                Text(stringResource(R.string.signin_prompt))
                Button(enabled = !state.busy, onClick = { vm.signIn(context) }) { Text(stringResource(R.string.continue_google)) }
                if (state.error != null) TextButton(enabled = !state.busy, onClick = vm::resumeSession) { Text(stringResource(R.string.retry_saved_session)) }
            } else if (!onboarded) {
                ProfileEditor(profile, state.busy, true, vm::saveProfile)
                TextButton(enabled = !state.busy, onClick = vm::logout) { Text(stringResource(R.string.sign_out)) }
            } else if (editor != null) {
                val demand = editor == "demand"
                val missingRecord = editingId != null && if (demand) state.demands.none { it.id == editingId } else state.listings.none { it.id == editingId }
                if (missingRecord) {
                    Text(stringResource(R.string.record_unavailable))
                    TextButton(enabled = !state.busy, onClick = { editor = null }) { Text(stringResource(R.string.back)) }
                } else RecordEditor(demand, editingId, state.listings.find { it.id == editingId }, state.demands.find { it.id == editingId }, state.busy,
                    seedCropType = demandSeedCrop,
                    onBack = { editor = null }, onSave = { crop, amount, unit, date, location, photo ->
                        if (demand) vm.saveDemand(editingId, DemandWrite(crop, amount, unit, date, location)) { editor = null }
                        else vm.saveListing(editingId, ListingWrite(crop, amount, unit, date, location, photo)) { editor = null }
                    })
            } else if (matchId != null) {
                TextButton(enabled = !state.busy, onClick = { matchId = null }) { Text(stringResource(R.string.back_to_matches)) }
                Text(stringResource(R.string.match_details), style = MaterialTheme.typography.titleLarge)
                val detail = state.detail?.takeIf { it.matchId == matchId }
                state.matches.find { it.matchId == matchId }?.let { Text("${it.counterpart.cropType} · ${it.counterpart.quantity} ${it.counterpart.unit}") }
                if (detail != null) {
                    Text(stringResource(R.string.fit_status, (detail.score * 100).roundToInt(), statusLabel(detail.status)))
                    if (detail.status in listOf("Confirmed", "Completed")) {
                        Text(detail.counterpartContact?.displayName ?: stringResource(R.string.contact_name_unavailable))
                        Text(detail.counterpartContact?.phone ?: stringResource(R.string.no_phone_shared))
                    } else Text(stringResource(R.string.contact_after_confirm))
                    if (detail.status == "Suggested") Button(enabled = !state.busy,
                        onClick = { vm.confirm(detail.matchId) }) { Text(stringResource(R.string.confirm_match)) }
                    if (detail.status == "Confirmed") Button(enabled = !state.busy,
                        onClick = { completeId = detail.matchId }) { Text(stringResource(R.string.mark_completed)) }
                    if (detail.status == "Completed" && detail.matchId !in state.ratedMatches) Row {
                        TextButton(enabled = !state.busy, onClick = { vm.rate(detail.matchId, true) }) { Text(stringResource(R.string.thumbs_up)) }
                        TextButton(enabled = !state.busy, onClick = { vm.rate(detail.matchId, false) }) { Text(stringResource(R.string.thumbs_down)) }
                    }
                }
                completeId?.takeIf { it == detail?.matchId && detail.status == "Confirmed" }?.let { id ->
                    AlertDialog(onDismissRequest = { completeId = null },
                        title = { Text(stringResource(R.string.complete_title)) },
                        text = { Text(stringResource(R.string.complete_text)) },
                        confirmButton = { TextButton(enabled = !state.busy, onClick = { completeId = null; vm.complete(id) }) { Text(stringResource(R.string.complete_exchange)) } },
                        dismissButton = { TextButton(onClick = { completeId = null }) { Text(stringResource(R.string.cancel)) } })
                }
                if (matchId in state.ratedMatches) Text(stringResource(R.string.already_rated))
                TextButton(enabled = !state.busy, onClick = { matchId?.let(vm::openMatch) }) { Text(stringResource(R.string.refresh_details)) }
            } else {
                if (tab != "Settings") TextButton(enabled = !state.busy, onClick = vm::refresh) { Text(stringResource(R.string.refresh)) }
                when (tab) {
                    "Home" -> {
                        Text(stringResource(if (farmer) R.string.home_title_farmer else R.string.home_title_buyer), style = MaterialTheme.typography.titleLarge)
                        Text(stringResource(if (farmer) R.string.home_sub_farmer else R.string.home_sub_buyer))
                        if (farmer && state.loaded && state.error == null && state.matches.isEmpty()) Text(stringResource(R.string.no_matches_farmer))
                        if (!farmer) {
                            val recommendations = BuyerRecommendations.find(state.listings, state.demands)
                            if (state.loaded && recommendations.isEmpty()) Text(stringResource(R.string.no_recommendations))
                            recommendations.forEach { RecommendedListingCard(it) }
                            val nearby = BuyerRecommendations.nearby(state.listings, state.demands, profile.searchRadiusKm,
                                recommendations.mapTo(mutableSetOf()) { it.listing.id })
                            Text(stringResource(R.string.other_products_within, profile.searchRadiusKm), style = MaterialTheme.typography.titleLarge)
                            if (state.loaded && state.demands.none { it.status == "Open" })
                                Text(stringResource(R.string.create_open_demand_hint))
                            else if (state.loaded && nearby.isEmpty())
                                Text(stringResource(R.string.no_nearby))
                            nearby.forEach { NearbyBuyerListingCard(it) }
                            if (state.matches.isNotEmpty()) Text(stringResource(R.string.active_matches), style = MaterialTheme.typography.titleLarge)
                        }
                        state.matches.forEach { item ->
                            Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp)) {
                                Text(item.counterpart.cropType, style = MaterialTheme.typography.titleMedium)
                                Text("${item.counterpart.quantity} ${item.counterpart.unit} · ${"%.1f".format(item.counterpart.distanceKm)} km")
                                Text(stringResource(R.string.fit_status, (item.score * 100).roundToInt(), statusLabel(item.status)))
                                Text(stringResource(if (farmer) R.string.needed_by else R.string.harvest_label, item.counterpart.relevantDate))
                                TextButton(enabled = !state.busy, onClick = { matchId = item.matchId; vm.openMatch(item.matchId) }) { Text(stringResource(R.string.view_match)) }
                            } }
                        }
                    }
                    "Listings" -> {
                        Text(stringResource(if (farmer) R.string.listings_farmer else R.string.listings_buyer), style = MaterialTheme.typography.titleLarge)
                        Button(enabled = !state.busy, onClick = { editingId = null; demandSeedCrop = null; editor = if (farmer) "listing" else "demand" }) {
                            Text(stringResource(if (farmer) R.string.add_listing else R.string.post_demand))
                        }
                        if (farmer) {
                            if (state.loaded && state.error == null && state.listings.isEmpty()) Text(stringResource(R.string.no_active_listings))
                            state.listings.forEach { item ->
                                ListingPhoto(item.photoUrl)
                                RecordCard(item.cropType, "${item.quantity} ${item.unit} · ${item.harvestDate} · ${statusLabel(item.status)}", state.busy,
                                    { editingId = item.id; editor = "listing" }, { deleteId = item.id })
                            }
                        } else {
                            if (state.loaded && state.error == null && state.demands.isEmpty()) Text(stringResource(R.string.no_open_demands))
                            state.demands.forEach { item -> RecordCard(item.cropType, "${item.quantityNeeded} ${item.unit} · ${item.deadline} · ${statusLabel(item.status)}", state.busy,
                                { editingId = item.id; editor = "demand" }, { deleteId = item.id }) }
                            Text(stringResource(R.string.available_produce), style = MaterialTheme.typography.titleLarge)
                            state.listings.forEach { item -> BrowseListingCard(item, state.busy) {
                                editingId = null; demandSeedCrop = item.cropType; editor = "demand"
                            } }
                            Text(stringResource(R.string.find_nearby), style = MaterialTheme.typography.titleLarge)
                            NearbyFilter(profile.searchRadiusKm, state.busy, vm::browse)
                            if (state.nearbySearchPerformed) Text(stringResource(R.string.nearby_results), style = MaterialTheme.typography.titleLarge)
                            if (state.nearbySearchPerformed && state.nearbyListings.isEmpty()) Text(stringResource(R.string.no_nearby_results))
                            state.nearbyListings.forEach { item ->
                                BrowseListingCard(item, state.busy) {
                                    editingId = null; demandSeedCrop = item.cropType; editor = "demand"
                                }
                            }
                        }
                    }
                    "Calendar" -> {
                        HarvestCalendar(state.listings)
                    }
                    "Settings" -> {
                        ProfileEditor(profile, state.busy, false, vm::saveProfile)
                        HorizontalDivider()
                        LanguagePicker()
                        HorizontalDivider()
                        Text(stringResource(R.string.device_security), style = MaterialTheme.typography.titleMedium)
                        if (!state.biometricEnabled) BiometricAction(stringResource(R.string.enable_biometric_device), !state.busy, vm::enableBiometrics, vm::biometricError)
                        else {
                            TextButton(enabled = !state.busy, onClick = vm::lock) { Text(stringResource(R.string.lock_app)) }
                            TextButton(enabled = !state.busy, onClick = vm::disableBiometrics) { Text(stringResource(R.string.turn_off_biometric)) }
                        }
                        Text(stringResource(R.string.biometric_note), style = MaterialTheme.typography.bodySmall)
                        HorizontalDivider()
                        TextButton(enabled = !state.busy, onClick = vm::logout) { Text(stringResource(R.string.sign_out)) }
                    }
                }
            }
            Spacer(Modifier.height(16.dp))
        }
    }
    deleteId?.let { id -> AlertDialog(onDismissRequest = { deleteId = null }, title = { Text(stringResource(R.string.remove_title)) },
        text = { Text(stringResource(R.string.remove_text)) },
        confirmButton = { TextButton(onClick = { deleteId = null; vm.delete(id, !farmer) }) { Text(stringResource(R.string.remove)) } },
        dismissButton = { TextButton(onClick = { deleteId = null }) { Text(stringResource(R.string.cancel)) } }) }
}

@Composable
private fun RecordCard(title: String, summary: String, busy: Boolean, edit: () -> Unit, remove: () -> Unit) {
    Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp)) {
        Text(title, style = MaterialTheme.typography.titleMedium)
        Text(summary)
        Row {
            TextButton(enabled = !busy, onClick = edit) { Text(stringResource(R.string.edit)) }
            TextButton(enabled = !busy, onClick = remove) { Text(stringResource(R.string.remove)) }
        }
    } }
}

@Composable
private fun BrowseListingCard(item: Listing, busy: Boolean, openDemand: () -> Unit) {
    Card(onClick = openDemand, enabled = !busy, modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(16.dp)) {
            ListingPhoto(item.photoUrl)
            Text(item.cropType, style = MaterialTheme.typography.titleMedium)
            Text("${item.quantity} ${item.unit} · ${item.harvestDate}")
        }
    }
}

@Composable
private fun RecommendedListingCard(item: BuyerRecommendation) {
    Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
        ListingPhoto(item.listing.photoUrl)
        Text(item.listing.cropType, style = MaterialTheme.typography.titleMedium)
        Text(stringResource(R.string.available_qty, item.listing.quantity, item.listing.unit))
        Text(stringResource(R.string.harvest_date, item.listing.harvestDate))
        Text(stringResource(R.string.km_from_demand, "%.1f".format(item.distanceKm)))
        Text(stringResource(R.string.matches_request, item.demand.quantityNeeded, item.demand.unit, item.demand.deadline),
            style = MaterialTheme.typography.bodySmall)
    } }
}

@Composable
private fun NearbyBuyerListingCard(item: NearbyBuyerListing) {
    Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
        ListingPhoto(item.listing.photoUrl)
        Text(item.listing.cropType, style = MaterialTheme.typography.titleMedium)
        Text(stringResource(R.string.available_qty, item.listing.quantity, item.listing.unit))
        Text(stringResource(R.string.harvest_date, item.listing.harvestDate))
        Text(stringResource(R.string.km_away, "%.1f".format(item.distanceKm)))
    } }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun HarvestCalendar(listings: List<Listing>) {
    val calendar = rememberDatePickerState()
    Text(stringResource(R.string.harvest_calendar), style = MaterialTheme.typography.titleLarge)
    Text(stringResource(R.string.calendar_hint))
    DatePicker(state = calendar, showModeToggle = false)
    val selectedDate = calendar.selectedDateMillis?.let { Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate().toString() }
    if (selectedDate == null) Text(stringResource(R.string.select_date))
    else {
        Text(selectedDate, style = MaterialTheme.typography.titleMedium)
        val dayListings = listings.filter { it.harvestDate == selectedDate }
        if (dayListings.isEmpty()) Text(stringResource(R.string.no_produce_date))
        dayListings.forEach { item -> Card(Modifier.fillMaxWidth()) { Column(Modifier.padding(16.dp)) {
            ListingPhoto(item.photoUrl)
            Text(item.cropType, style = MaterialTheme.typography.titleMedium)
            Text("${item.quantity} ${item.unit} · ${statusLabel(item.status)}")
        } } }
    }
}
