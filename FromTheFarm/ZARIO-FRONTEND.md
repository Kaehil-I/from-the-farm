# Zario — Part 2 frontend implementation

## What is connected
- Google Credential Manager → Firebase Authentication; saved Firebase session restoration and sign-out.
- Backend session/profile validation before opening authenticated farm screens.
- Farmer listing and buyer demand create, read, edit and remove operations.
- ISO date picker, quantity/coordinate validation, retained input after failures, duplicate-tap prevention.
- Optional gallery photo selection, JPEG compression (maximum 256 KiB), base64 request and photo display.
- Match feed sorted by score, selected detail, contact gating, confirmation, completion and rating controls.
- Greg's existing SettingsScreen is retained, with Firebase logout connected; preference persistence remains Greg's work.

The original prototype screens remain available as source/design references. The active shell now uses the API-backed screens and never substitutes sample records on network failures.

## Configure the backend address
In this project's local.properties, preserve sdk.dir and add:

    API_BASE_URL=https://YOUR-VERCEL-HOST/AGREED-PREFIX/

Replace both placeholders with Kaehil's real URL and prefix. Do not assume /api/v1/ unless he confirms it. Gradle -PAPI_BASE_URL overrides local.properties. Rebuild after changing the URL.

The existing app/google-services.json must belong to the same Firebase project as the backend. Enable Google sign-in and register the SHA-1 of your local debug key. Do not use this task's verification key fingerprint.

Without an API URL, successful Firebase sign-in shows “Google sign-in succeeded” and a profile-loading error. This is intentional: Google authentication can be tested before the backend is deployed. It does not create a MongoDB profile until the API accepts the session.

## Backend handover — Kaehil and Greg
All requests below carry Authorization: Bearer <Firebase ID token>.

- POST auth/session { deviceLanguage } → { userId, onboardingComplete }
- GET users/me and PUT users/me → Profile (see FarmApi.kt).
- GET listings?mine=true; buyer listing search accepts cropType, maxDistanceKm, latitude, longitude.
- POST/PUT listings: cropType, quantity, unit, harvestDate (YYYY-MM-DD), location {latitude,longitude}, optional photoBase64.
- GET demands?mine=true; POST/PUT demands: cropType, quantityNeeded, unit, deadline, location.
- Listing/demand responses use id, not MongoDB _id. Listings may include photoUrl (HTTPS URL or data URI).
- DELETE listings/{id} and demands/{id} → 204.
- GET matches → array of {matchId, score, counterpart:{cropType,quantity,unit,distanceKm,relevantDate},status}.
- GET matches/{id} → {matchId,score,status,counterpartContact:{displayName,phone}}; contact may be null before confirmation.
- POST matches/{id}/confirm and /complete → 204.
- POST matches/{id}/rating {thumbsUp} → 204; duplicate rating → 409.

Kaehil: deployment, Firebase token verification, scoring/feed/detail/transitions and CI/CD.
Greg: listing/demand CRUD, MongoDB ownership and validation, photo creation/update support, profile/settings endpoints and SettingsScreen persistence.
Kyra: coordinate the included frontend regression tests with the main Android test suite; README and demo evidence remain hers.

Image replacement on update requires Greg's migrated endpoint to honour photoBase64; the last C# endpoint ignored photo edits. The frontend does not implement removal of a previously saved photo because there is no agreed removal contract. Location entry is manual. Offline sync, translated UI and notification delivery remain outside this implementation. Biometric device unlock was added at Zario's explicit request.

## Manual verification
1. Sign in on a Google Play emulator, confirm a user appears in Firebase Authentication, cancel and retry sign-in, sign out.
2. Configure the deployed API; complete onboarding with Farmer/Buyer role; restart and reload the same profile.
3. Create, edit and remove a listing/demand; reload and confirm actual persistence.
4. Try blank crop, negative quantity, invalid date and coordinates; disconnect networking and confirm inputs remain.
5. Select a small image and verify returned photoUrl renders; test edits after Greg implements image replacement.
6. Use two accounts and compatible records; open distinct matches; verify contacts hidden before confirmation.
7. Confirm, complete, rate; ensure premature/repeated actions are blocked.
8. Kyra must arrange the physical Android device required for the final demo.

## Suggested commits — you execute these
1. feat(android): add authenticated REST contract and Firebase Google sign-in
   app/build.gradle.kts, build.gradle.kts, AndroidManifest.xml, data/FarmApi.kt, data/FarmRepository.kt, data/SampleData.kt, ui/screens/HomeScreen.kt, ui/screens/MyListingsScreen.kt (rename sample Listing to PreviewListing to avoid model collisions)
2. feat(android): manage sessions and authenticated farm actions
   ui/FarmViewModel.kt
3. feat(android): add validated listing and demand editors with photos
   data/FormValidation.kt, ui/screens/LiveForms.kt, ui/screens/ListingPhoto.kt
4. feat(android): replace prototype navigation with live farm flows
   ui/navigation/FarmNavHost.kt
5. test(android): cover frontend authentication and match lifecycle
   app/src/test (coordinate with Kyra)
6. docs(android): document Zario frontend setup and backend handover
   ZARIO-FRONTEND.md

Keep this order: the API/dependencies and preview rename must land before the ViewModel, forms and navigation. Run assembleDebug at each staged boundary before pushing. Do not stage generated build/.gradle folders, local.properties, or unrelated pre-existing staged changes.



## Verification — 16 September 2026
An identical source copy passed assembleDebug, testDebugUnitTest (23 tests, zero failures/errors), and lintDebug. Verification used SDK 34, JDK 17, Firebase BoM 33.7.0, and Google Services plugin 4.4.2. A temporary drive alias worked around Java path-access restrictions in the agent environment. Only the verification copy used an isolated debug signing key; your local.properties and signing settings were preserved.

Build/tests do not establish live Firebase sign-in or MongoDB persistence. Run the manual checks above in Android Studio with your own debug key and the deployed API. Lint has warnings, including prototype deprecations; no blocking lint errors.

The repository already contained staged deletions when this work began. No index/staging operations, commits, or pushes were performed. Review existing staging before making your commits.


## Biometric device unlock — 17 September 2026
Google sign-in establishes identity first. Strong fingerprint/supported face authentication can then unlock the saved Firebase session locally. It does not create Firebase credentials or bypass the Vercel API. The device opt-in is keyed by Firebase UID and cleared at sign-out. No biometric templates or new token copies are stored by this feature. This is an app-access gate using the OS prompt, not encrypted Firebase credential storage.

Enable biometric unlock through the profile-pending screen or the separate device-unlock control above Greg's SettingsScreen. Cancelling enrollment authentication keeps it disabled. Cancelling unlock keeps the app locked. Backgrounding/restarting the app requires unlock when enabled. Fresh Google sign-in is available if biometrics are unavailable. Turn-off is available only while the app is unlocked.

### Emulator steps
1. Sync/rebuild/run the updated app and sign in with Google.
2. In emulator Android Settings, open Security & privacy → Device unlock → Fingerprint (labels may differ). Set a PIN and start fingerprint enrollment.
3. Open emulator Extended Controls (…) → Fingerprint. Select Finger 1 and press Touch sensor repeatedly as Android requests enrollment scans.
4. Return to the app; choose Enable biometric unlock. When the prompt appears, use Extended Controls → Fingerprint → Finger 1 → Touch sensor.
5. Choose Lock app, test Cancel (must remain locked), then authenticate with Finger 1.
6. Background/reopen and restart the app; both should require unlock. Turn off biometric unlock while unlocked to opt out.
7. Without Vercel, the profile remains pending after successful local unlock. This is expected; the backend still validates Firebase tokens before providing farm data.

Biometric prompt/enrollment and lifecycle behavior still need the manual emulator checks; unit tests simulate the successful OS callback and access-gate state.

Final verification (17 September 2026): assembleDebug, testDebugUnitTest and lintDebug succeeded against an identical copy of the final app source. All 25 unit tests passed. The build used an isolated verification signing key; use Android Studio with your own configured key for Firebase emulator testing. The operating-system biometric prompt and live Vercel/database flow still require manual acceptance testing.
