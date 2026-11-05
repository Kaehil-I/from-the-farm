package com.fromthefarm.app.ui.screens

import android.Manifest
import android.annotation.SuppressLint
import android.content.pm.PackageManager
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import com.fromthefarm.app.R
import com.fromthefarm.app.data.*
import kotlin.math.roundToInt
import java.time.LocalDate
import java.time.Instant
import java.time.ZoneOffset
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.ui.platform.LocalContext
import androidx.core.content.ContextCompat
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import kotlinx.coroutines.tasks.await
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.Dispatchers
import java.io.File

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun RecordEditor(demand: Boolean, id: String?, listing: Listing?, request: Demand?, busy: Boolean,
    seedCropType: String? = null, onBack: () -> Unit, onSave: (String, Double, String, String, Location, String?) -> Unit) {
    var crop by rememberSaveable(id, demand, seedCropType) { mutableStateOf(if (demand) request?.cropType ?: seedCropType.takeIf { id == null }.orEmpty() else listing?.cropType ?: "") }
    var quantity by rememberSaveable(id, demand) { mutableStateOf(if (demand) request?.quantityNeeded?.toString() ?: "" else listing?.quantity?.toString() ?: "") }
    var unit by rememberSaveable(id, demand) { mutableStateOf(if (demand) request?.unit ?: "kg" else listing?.unit ?: "kg") }
    var date by rememberSaveable(id, demand) { mutableStateOf(if (demand) request?.deadline ?: "" else listing?.harvestDate ?: "") }
    val location = if (demand) request?.location else listing?.location
    var latitude by rememberSaveable(id, demand) { mutableStateOf(location?.latitude?.toString() ?: "") }
    var longitude by rememberSaveable(id, demand) { mutableStateOf(location?.longitude?.toString() ?: "") }
    var error by rememberSaveable(id, demand) { mutableStateOf<String?>(null) }
    var showDatePicker by rememberSaveable { mutableStateOf(false) }
    var photoPath by rememberSaveable(id, demand) { mutableStateOf<String?>(null) }
    var photoBusy by remember { mutableStateOf(false) }
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
        if (uri != null) scope.launch {
            photoBusy = true
            try { photoPath = withContext(Dispatchers.IO) { PhotoTools.prepare(context, uri).absolutePath }; error = null }
            catch (_: Exception) { error = context.getString(R.string.photo_open_error) }
            finally { photoBusy = false }
        }
    }
    TextButton(enabled = !busy, onClick = onBack) { Text(stringResource(R.string.back)) }
    Text(stringResource(when {
        demand && id == null -> R.string.title_create_demand
        demand -> R.string.title_edit_demand
        id == null -> R.string.title_create_listing
        else -> R.string.title_edit_listing
    }), style = MaterialTheme.typography.titleLarge)
    Field(stringResource(R.string.field_crop_type), crop, busy) { crop = it }
    Field(stringResource(R.string.field_quantity), quantity, busy) { quantity = it }
    Field(stringResource(R.string.field_unit), unit, busy) { unit = it }
    Field(stringResource(if (demand) R.string.field_deadline else R.string.field_harvest_date), date, busy) { date = it }
    TextButton(enabled = !busy, onClick = { showDatePicker = true }) { Text(stringResource(R.string.choose_date)) }
    LocationFields(latitude, longitude, busy,
        onLatitudeChange = { latitude = it }, onLongitudeChange = { longitude = it })
    if (!demand) {
        ListingPhoto(photoPath ?: listing?.photoUrl)
        TextButton(enabled = !busy && !photoBusy, onClick = { picker.launch("image/*") }) {
            Text(stringResource(if (photoBusy) R.string.photo_preparing else R.string.choose_photo))
        }
        if (photoPath != null) TextButton(enabled = !busy, onClick = { photoPath = null }) { Text(stringResource(R.string.discard_photo)) }
    }
    error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
    Button(enabled = !busy && !photoBusy, onClick = {
        error = FormValidation.validate(crop.trim(), quantity.trim(), unit.trim(), date.trim(), latitude.trim(), longitude.trim(), demand)
        if (error == null) {
            val photo = runCatching { photoPath?.let { android.util.Base64.encodeToString(File(it).readBytes(), android.util.Base64.NO_WRAP) } }
            if (photo.isFailure) error = context.getString(R.string.photo_reselect)
            else onSave(CropNames.normalize(crop), quantity.trim().toDouble(), unit.trim(), date.trim(), Location(latitude.trim().toDouble(), longitude.trim().toDouble()), photo.getOrNull())
        }
    }) { Text(stringResource(if (busy) R.string.saving else R.string.save)) }
    if (showDatePicker) {
        val selected = runCatching { LocalDate.parse(date).atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli() }.getOrNull()
        val datePicker = rememberDatePickerState(initialSelectedDateMillis = selected)
        DatePickerDialog(onDismissRequest = { showDatePicker = false }, confirmButton = {
            TextButton(enabled = datePicker.selectedDateMillis != null, onClick = {
                datePicker.selectedDateMillis?.let { date = Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate().toString() }
                showDatePicker = false
            }) { Text(stringResource(R.string.use_date)) }
        }, dismissButton = { TextButton(onClick = { showDatePicker = false }) { Text(stringResource(R.string.cancel)) } }) { DatePicker(state = datePicker) }
    }
}

@Composable
fun NearbyFilter(radius: Int, busy: Boolean, search: (String?, Int, Location) -> Unit) {
    var crop by rememberSaveable { mutableStateOf("") }
    var latitude by rememberSaveable { mutableStateOf("") }
    var longitude by rememberSaveable { mutableStateOf("") }
    var error by rememberSaveable { mutableStateOf<Int?>(null) }
    Field(stringResource(R.string.field_crop_blank), crop, busy) { crop = it }
    LocationFields(latitude, longitude, busy,
        onLatitudeChange = { latitude = it }, onLongitudeChange = { longitude = it })
    Text(stringResource(R.string.search_within, radius))
    error?.let { Text(stringResource(it), color = MaterialTheme.colorScheme.error) }
    Button(enabled = !busy, onClick = {
        val lat = latitude.trim().toDoubleOrNull()
        val lon = longitude.trim().toDoubleOrNull()
        error = if (lat == null || !lat.isFinite() || lat !in -90.0..90.0 || lon == null || !lon.isFinite() || lon !in -180.0..180.0)
            R.string.nearby_invalid_coords else null
        if (error == null) search(crop.takeIf { it.isNotBlank() }?.let(CropNames::normalize), radius, Location(lat!!, lon!!))
    }) { Text(stringResource(R.string.find_nearby)) }
}

@SuppressLint("MissingPermission")
@Composable
private fun LocationFields(latitude: String, longitude: String, busy: Boolean,
    onLatitudeChange: (String) -> Unit, onLongitudeChange: (String) -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val locationClient = remember(context) { LocationServices.getFusedLocationProviderClient(context) }
    var locating by remember { mutableStateOf(false) }
    var showManual by rememberSaveable { mutableStateOf(latitude.isNotBlank() && longitude.isNotBlank()) }
    var locationMessage by rememberSaveable { mutableStateOf<Int?>(null) }

    val findLocation: () -> Unit = {
        scope.launch {
            locating = true
            locationMessage = null
            try {
                val token = CancellationTokenSource()
                val result = locationClient.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, token.token).await()
                if (result == null) {
                    locationMessage = R.string.location_unknown_enable
                    showManual = true
                } else {
                    onLatitudeChange(String.format(java.util.Locale.US, "%.6f", result.latitude))
                    onLongitudeChange(String.format(java.util.Locale.US, "%.6f", result.longitude))
                    locationMessage = R.string.location_selected
                    showManual = false
                }
            } catch (_: Exception) {
                locationMessage = R.string.location_unknown_check
                showManual = true
            } finally {
                locating = false
            }
        }
    }
    val permissionLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { grants ->
        if (grants[Manifest.permission.ACCESS_FINE_LOCATION] == true || grants[Manifest.permission.ACCESS_COARSE_LOCATION] == true) {
            findLocation()
        } else {
            locationMessage = R.string.location_permission_denied
            showManual = true
        }
    }

    Text(stringResource(R.string.location_title), style = MaterialTheme.typography.titleMedium)
    Text(stringResource(R.string.location_hint))
    Button(enabled = !busy && !locating, onClick = {
        val fineGranted = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED
        val coarseGranted = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED
        if (fineGranted || coarseGranted) findLocation()
        else permissionLauncher.launch(arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION))
    }) { Text(stringResource(if (locating) R.string.finding_location else R.string.use_my_location)) }
    if (latitude.isNotBlank() && longitude.isNotBlank() && !showManual) {
        Text(stringResource(R.string.selected_location, latitude, longitude), style = MaterialTheme.typography.bodySmall)
    }
    locationMessage?.let {
        Text(stringResource(it), color = if (it == R.string.location_selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error)
    }
    TextButton(enabled = !busy && !locating, onClick = { showManual = !showManual }) {
        Text(stringResource(if (showManual) R.string.hide_manual else R.string.enter_manual))
    }
    if (showManual) {
        Field(stringResource(R.string.latitude), latitude, busy || locating, onLatitudeChange)
        Field(stringResource(R.string.longitude), longitude, busy || locating, onLongitudeChange)
    }
}

/** Role values are sent to the API in English; only the label shown is translated. */
@Composable
private fun roleLabel(role: String): String = when (role) {
    "Farmer" -> stringResource(R.string.role_farmer)
    "Buyer" -> stringResource(R.string.role_buyer)
    else -> role
}

@Composable
fun ProfileEditor(profile: Profile, busy: Boolean, onboarding: Boolean, save: (ProfileUpdate) -> Unit) {
    val context = LocalContext.current
    var role by rememberSaveable(profile.userId, profile.role) { mutableStateOf(profile.role ?: "Farmer") }
    var language by rememberSaveable(profile.userId, profile.language) { mutableStateOf(profile.language) }
    var radius by rememberSaveable(profile.userId, profile.searchRadiusKm) { mutableStateOf(profile.searchRadiusKm.toFloat()) }
    var phone by rememberSaveable(profile.userId, profile.phone) { mutableStateOf(profile.phone ?: "") }
    var notifications by rememberSaveable(profile.userId, profile.notificationsEnabled) { mutableStateOf(profile.notificationsEnabled) }
    var error by rememberSaveable { mutableStateOf<String?>(null) }
    val roleChanged = profile.role != null && role != profile.role
    Text(stringResource(if (onboarding) R.string.profile_setup else R.string.tab_settings), style = MaterialTheme.typography.titleLarge)
    Text(stringResource(if (onboarding) R.string.choose_start else R.string.active_mode), style = MaterialTheme.typography.titleMedium)
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        listOf("Farmer", "Buyer").forEach { value -> FilterChip(selected = role == value, enabled = !busy, onClick = { role = value }, label = { Text(roleLabel(value)) }) }
    }
    if (!onboarding) Text(stringResource(R.string.mode_switch_note))
    Text(stringResource(R.string.preferred_language))
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        // Language names stay in their own language so they can still be read after a wrong choice.
        listOf("en" to "English", "zu" to "isiZulu", "af" to "Afrikaans").forEach { (code, name) ->
            FilterChip(selected = language == code, enabled = !busy, onClick = { language = code }, label = { Text(name) })
        }
    }
    Text(stringResource(R.string.search_radius, radius.roundToInt()))
    Slider(value = radius, onValueChange = { radius = it }, enabled = !busy, valueRange = 1f..100f, steps = 98)
    Field(stringResource(R.string.field_phone), phone, busy) { phone = it }
    Text(stringResource(R.string.phone_shared_note))
    Row { Checkbox(checked = notifications, enabled = !busy, onCheckedChange = { notifications = it }); Text(stringResource(R.string.notification_preference)) }
    error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
    Button(enabled = !busy, onClick = {
        val clean = phone.trim()
        error = if (clean.isNotEmpty() && !clean.matches(Regex("[+0-9 ()-]{7,25}"))) context.getString(R.string.phone_invalid) else null
        if (error == null) save(ProfileUpdate(role, language, radius.roundToInt(), notifications, profile.biometricLockEnabled, clean.ifBlank { null }))
    }) { Text(if (onboarding) stringResource(R.string.continue_label) else if (roleChanged) stringResource(R.string.switch_to_role, roleLabel(role)) else stringResource(R.string.save_preferences)) }
}

@Composable
private fun Field(label: String, value: String, busy: Boolean, change: (String) -> Unit) {
    OutlinedTextField(value = value, onValueChange = change, label = { Text(label) }, singleLine = true,
        enabled = !busy, modifier = Modifier.fillMaxWidth())
}
