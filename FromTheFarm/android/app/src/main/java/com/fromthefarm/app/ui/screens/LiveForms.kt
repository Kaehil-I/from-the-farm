package com.fromthefarm.app.ui.screens

import android.Manifest
import android.annotation.SuppressLint
import android.content.pm.PackageManager
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
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
            catch (_: Exception) { error = "Could not open this photo. Choose a smaller JPEG or PNG image." }
            finally { photoBusy = false }
        }
    }
    TextButton(enabled = !busy, onClick = onBack) { Text("Back") }
    Text("${if (id == null) "Create" else "Edit"} ${if (demand) "demand request" else "listing"}", style = MaterialTheme.typography.titleLarge)
    Field("Crop type", crop, busy) { crop = it }
    Field("Quantity", quantity, busy) { quantity = it }
    Field("Unit (e.g. kg)", unit, busy) { unit = it }
    Field(if (demand) "Deadline (YYYY-MM-DD)" else "Harvest date (YYYY-MM-DD)", date, busy) { date = it }
    TextButton(enabled = !busy, onClick = { showDatePicker = true }) { Text("Choose date") }
    LocationFields(latitude, longitude, busy,
        onLatitudeChange = { latitude = it }, onLongitudeChange = { longitude = it })
    if (!demand) {
        ListingPhoto(photoPath ?: listing?.photoUrl)
        TextButton(enabled = !busy && !photoBusy, onClick = { picker.launch("image/*") }) {
            Text(if (photoBusy) "Preparing photo…" else "Choose listing photo")
        }
        if (photoPath != null) TextButton(enabled = !busy, onClick = { photoPath = null }) { Text("Discard selected photo") }
    }
    error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
    Button(enabled = !busy && !photoBusy, onClick = {
        error = FormValidation.validate(crop.trim(), quantity.trim(), unit.trim(), date.trim(), latitude.trim(), longitude.trim(), demand)
        if (error == null) {
            val photo = runCatching { photoPath?.let { android.util.Base64.encodeToString(File(it).readBytes(), android.util.Base64.NO_WRAP) } }
            if (photo.isFailure) error = "Select your photo again before saving."
            else onSave(CropNames.normalize(crop), quantity.trim().toDouble(), unit.trim(), date.trim(), Location(latitude.trim().toDouble(), longitude.trim().toDouble()), photo.getOrNull())
        }
    }) { Text(if (busy) "Saving…" else "Save") }
    if (showDatePicker) {
        val selected = runCatching { LocalDate.parse(date).atStartOfDay(ZoneOffset.UTC).toInstant().toEpochMilli() }.getOrNull()
        val picker = rememberDatePickerState(initialSelectedDateMillis = selected)
        DatePickerDialog(onDismissRequest = { showDatePicker = false }, confirmButton = {
            TextButton(enabled = picker.selectedDateMillis != null, onClick = {
                picker.selectedDateMillis?.let { date = Instant.ofEpochMilli(it).atZone(ZoneOffset.UTC).toLocalDate().toString() }
                showDatePicker = false
            }) { Text("Use date") }
        }, dismissButton = { TextButton(onClick = { showDatePicker = false }) { Text("Cancel") } }) { DatePicker(state = picker) }
    }
}

@Composable
fun NearbyFilter(radius: Int, busy: Boolean, search: (String?, Int, Location) -> Unit) {
    var crop by rememberSaveable { mutableStateOf("") }
    var latitude by rememberSaveable { mutableStateOf("") }
    var longitude by rememberSaveable { mutableStateOf("") }
    var error by rememberSaveable { mutableStateOf<String?>(null) }
    Field("Crop type (blank for all)", crop, busy) { crop = it }
    LocationFields(latitude, longitude, busy,
        onLatitudeChange = { latitude = it }, onLongitudeChange = { longitude = it })
    Text("Search within $radius km. Change the radius in Settings.")
    error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
    Button(enabled = !busy, onClick = {
        val lat = latitude.trim().toDoubleOrNull()
        val lon = longitude.trim().toDoubleOrNull()
        error = if (lat == null || !lat.isFinite() || lat !in -90.0..90.0 || lon == null || !lon.isFinite() || lon !in -180.0..180.0)
            "Enter valid latitude and longitude for the search location." else null
        if (error == null) search(crop.takeIf { it.isNotBlank() }?.let(CropNames::normalize), radius, Location(lat!!, lon!!))
    }) { Text("Find nearby produce") }
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
    var locationMessage by rememberSaveable { mutableStateOf<String?>(null) }

    val findLocation: () -> Unit = {
        scope.launch {
            locating = true
            locationMessage = null
            try {
                val token = CancellationTokenSource()
                val result = locationClient.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, token.token).await()
                if (result == null) {
                    locationMessage = "Could not determine your location. Turn on Location in Android Settings or enter it manually."
                    showManual = true
                } else {
                    onLatitudeChange(String.format(java.util.Locale.US, "%.6f", result.latitude))
                    onLongitudeChange(String.format(java.util.Locale.US, "%.6f", result.longitude))
                    locationMessage = "Current location selected."
                    showManual = false
                }
            } catch (_: Exception) {
                locationMessage = "Could not determine your location. Check Location in Android Settings or enter it manually."
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
            locationMessage = "Location permission was not granted. You can enter the coordinates manually."
            showManual = true
        }
    }

    Text("Location", style = MaterialTheme.typography.titleMedium)
    Text("Use your device location for accurate nearby matching.")
    Button(enabled = !busy && !locating, onClick = {
        val fineGranted = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED
        val coarseGranted = ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED
        if (fineGranted || coarseGranted) findLocation()
        else permissionLauncher.launch(arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION))
    }) { Text(if (locating) "Finding location…" else "Use my current location") }
    if (latitude.isNotBlank() && longitude.isNotBlank() && !showManual) {
        Text("Selected location: $latitude, $longitude", style = MaterialTheme.typography.bodySmall)
    }
    locationMessage?.let {
        Text(it, color = if (it == "Current location selected.") MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error)
    }
    TextButton(enabled = !busy && !locating, onClick = { showManual = !showManual }) {
        Text(if (showManual) "Hide manual coordinates" else "Enter coordinates manually")
    }
    if (showManual) {
        Field("Latitude", latitude, busy || locating, onLatitudeChange)
        Field("Longitude", longitude, busy || locating, onLongitudeChange)
    }
}

@Composable
fun ProfileEditor(profile: Profile, busy: Boolean, onboarding: Boolean, save: (ProfileUpdate) -> Unit) {
    var role by rememberSaveable(profile.userId, profile.role) { mutableStateOf(profile.role ?: "Farmer") }
    var language by rememberSaveable(profile.userId, profile.language) { mutableStateOf(profile.language) }
    var radius by rememberSaveable(profile.userId, profile.searchRadiusKm) { mutableStateOf(profile.searchRadiusKm.toFloat()) }
    var phone by rememberSaveable(profile.userId, profile.phone) { mutableStateOf(profile.phone ?: "") }
    var notifications by rememberSaveable(profile.userId, profile.notificationsEnabled) { mutableStateOf(profile.notificationsEnabled) }
    var error by rememberSaveable { mutableStateOf<String?>(null) }
    val roleChanged = profile.role != null && role != profile.role
    Text(if (onboarding) "Set up your profile" else "Settings", style = MaterialTheme.typography.titleLarge)
    Text(if (onboarding) "Choose how you want to start" else "Active mode", style = MaterialTheme.typography.titleMedium)
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        listOf("Farmer", "Buyer").forEach { value -> FilterChip(selected = role == value, enabled = !busy, onClick = { role = value }, label = { Text(value) }) }
    }
    if (!onboarding) Text("Switch modes without signing out. Your listings and demand requests stay connected to this account.")
    Text("Preferred language")
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        listOf("en" to "English", "zu" to "isiZulu", "af" to "Afrikaans").forEach { (code, name) ->
            FilterChip(selected = language == code, enabled = !busy, onClick = { language = code }, label = { Text(name) })
        }
    }
    Text("Search radius: ${radius.roundToInt()} km")
    Slider(value = radius, onValueChange = { radius = it }, enabled = !busy, valueRange = 1f..100f, steps = 98)
    Field("Phone number (optional)", phone, busy) { phone = it }
    Text("Your phone number is shared with confirmed matches.")
    Row { Checkbox(checked = notifications, enabled = !busy, onCheckedChange = { notifications = it }); Text("Save notification preference") }
    error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
    Button(enabled = !busy, onClick = {
        val clean = phone.trim()
        error = if (clean.isNotEmpty() && !clean.matches(Regex("[+0-9 ()-]{7,25}"))) "Enter a valid phone number or leave it blank." else null
        if (error == null) save(ProfileUpdate(role, language, radius.roundToInt(), notifications, profile.biometricLockEnabled, clean.ifBlank { null }))
    }) { Text(if (onboarding) "Continue" else if (roleChanged) "Switch to $role" else "Save preferences") }
}

@Composable
private fun Field(label: String, value: String, busy: Boolean, change: (String) -> Unit) {
    OutlinedTextField(value = value, onValueChange = change, label = { Text(label) }, singleLine = true,
        enabled = !busy, modifier = Modifier.fillMaxWidth())
}
