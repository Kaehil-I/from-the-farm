# From The Farm

**From The Farm** matches South African smallholder farmers' produce listings with nearby buyer demand requests, scored by crop match, distance, quantity fit and harvest timing. The project has two parts:

- **`FromTheFarm.Api`** — ASP.NET Core 8 Web API, MongoDB Atlas, Firebase ID token authentication.
- **`android`** — Kotlin + Jetpack Compose app. Google sign-in via Firebase, live CRUD against the API, a scored match feed, and optional biometric device unlock.

Part 1's UI-only prototype (mock data, no networking) has been replaced end to end: both sides are now live and talk to each other over a real REST contract. Jetpack Compose is Android's recommended modern toolkit for native, state-driven user interfaces (Android Developers, n.d.-a), while ASP.NET Core supplies the controller, routing and dependency-injection structure used by the API (Microsoft, 2026).

## Video walkthrough

[Watch the demo](https://youtu.be/6OHo6i-GAxo)

## Development repository

This repository is the submitted deliverable, with the team's full commit history. Day-to-day development happened on [Kaehil-I/from-the-farm](https://github.com/Kaehil-I/from-the-farm), including this repository's own commit history — pull request reviews, individual GitHub Actions run logs and issue discussion live there rather than transferring with a branch mirror. The live deployment on Render is also connected to that repository.

## Documentation

- [`Documentation/Release-Notes.md`](Documentation/Release-Notes.md) — what changed since Part 2, by team member.
- [`Documentation/Test-Matrix.md`](Documentation/Test-Matrix.md) — manual test matrix, regression checklist and translation checklist, with what has and has not been run.
- [`Documentation/Offline-Sync-Demo.md`](Documentation/Offline-Sync-Demo.md) — steps to demonstrate and verify offline creation and sync.

## How to open

### Backend (`FromTheFarm.Api`)

Run these from the `FromTheFarm` folder.

1. Requires the .NET 8 SDK.
2. Set the MongoDB Atlas connection string with user-secrets (never commit a real connection string):

   ```
   dotnet user-secrets set "MongoDb:ConnectionString" "<atlas connection string>" --project FromTheFarm.Api
   ```

   Special characters in the database password must be percent-encoded in the URI (for example `#` becomes `%23`), and the `<` `>` placeholders from Atlas's template must be removed. Atlas must also allow your IP under Network Access.
3. `Firebase:ProjectId` is already set in `appsettings.json` — it is a public identifier, not a secret.
4. `dotnet run --project FromTheFarm.Api`, then call `GET /api/v1/health`. It returns `healthy` once the Mongo ping succeeds. Swagger is served at `/swagger` in Development only.

### Android (`android`)

1. Open the `android` folder (not the repository root) in Android Studio.
2. In `android/local.properties`, next to `sdk.dir`, set the API address, including the `/api/v1/` prefix (the Retrofit endpoints in `FarmApi.kt` don't repeat it):

   ```
   API_BASE_URL=https://<your-api-host>/api/v1/
   ```

   (`-PAPI_BASE_URL` on the Gradle command line overrides this.) Without a valid HTTPS URL here, `FarmRepository` throws with a clear message rather than silently failing.
3. `android/app/google-services.json` must belong to the same Firebase project as the backend, with Google sign-in enabled and your local debug key's SHA-1 registered — sign-in fails otherwise. CI can still compile without this file; the Google Services plugin is only applied when it's present.
4. Gradle sync, then run on an emulator or device. Without a configured API URL, Google sign-in still succeeds (proving Firebase auth works) but profile loading fails — this is expected, not a bug.

## How the app works, end to end

- A user signs in with Google. Firebase hands back a signed ID token proving who they are.
- Every request from the app to the API carries that token; the API re-validates it against Google before trusting it, and reads the user's ID out of the token itself — never from anything the app just typed in.
- First sign-in only: the user picks **Farmer** or **Buyer**, a language, a search radius, and optionally a phone number.
- Users can switch between **Farmer** and **Buyer** modes without signing out. Their listings and demand requests remain connected to the same account.
- The API validates every listing, demand and profile write on the server, and returns 403 if a user tries to edit or delete a record they don't own. Creating a listing requires the Farmer role and creating a demand requires the Buyer role, so the app's hidden buttons are backed by the API, not just hidden in the UI.
- **Farmers** manage produce listings (crop, quantity, harvest date, location, optional photo). Photos are selected from the device and downscaled before upload, and editing a listing can replace its photo.
- **Buyers** manage demand requests and browse active produce as tappable cards. Selecting a listing opens a new demand with only the crop type prefilled, leaving the buyer's quantity, deadline and location independent.
- Location forms use the device's current location when permission is granted and retain manual latitude/longitude entry as a fallback.
- Nearby searches use the configured radius and case-insensitive crop filtering. Search results are stored separately from the complete catalogue, so a search cannot remove unrelated products from Home, Demand or Calendar.
- Buyer Home displays **Products matching your demands** first. Crop comparison ignores case. A second **Other products within range** section shows remaining products nearest-first, using the closest open-demand location and preventing duplicate cards.
- The Calendar tab uses an interactive Material 3 date picker and displays the produce available on the selected harvest date.
- **Matching**: `MatchingService` scores every listing/demand pair — 35% distance, 30% crop type (all-or-nothing), 20% quantity fit, 15% freshness (harvest vs. deadline, decaying to zero 14 days late). Anything outside the search radius, a different crop, or scoring below 0.4 overall is excluded entirely rather than shown as a weak match.
- Matches move through a fixed lifecycle the app enforces in order: **Suggested** (contact details hidden) → **Confirmed** (contact details unlock) → **Completed** → **Rated**. You can't skip a stage.
- Optional **biometric unlock** is a local, per-device app-access lock on top of an already-saved Firebase session — not a separate account, and not encrypted credential storage.

Firebase Authentication provides the federated Google identity flow used by the app (Google Firebase, n.d.-b). It requires the correct Android configuration, an enabled Google provider and the signing certificate SHA-1 (Google Firebase, n.d.-a). Biometric access requires compatible hardware and enrolled biometrics on the device (Android Developers, n.d.-b). MongoDB Atlas requires both a database user and an allowed network route before an application can connect; database-user permissions are managed separately from Atlas website accounts (MongoDB, n.d.-a; MongoDB, n.d.-b).

## Project structure

```
FromTheFarm.Api/
├── Dockerfile                        — multi-stage .NET 8 build, used for the Render deployment
├── Program.cs                        — DI, Mongo client, Firebase JWT bearer auth, Swagger
├── Controllers/
│   ├── AuthController.cs             — POST auth/session (create-or-fetch profile)
│   ├── UsersController.cs            — GET/PUT users/me (profile, onboarding, mode switch)
│   ├── ListingsController.cs         — farmer listings CRUD, buyer distance search
│   ├── DemandsController.cs          — buyer demand requests CRUD
│   ├── MatchesController.cs          — scored feed, detail, confirm/complete/rate
│   └── HealthController.cs           — unauthenticated Mongo ping, also reports the deployed commit
├── Models/                           — Listing, DemandRequest, MatchDocument, Rating, UserProfile (has OnboardingComplete), GeoLocation
└── Services/
    ├── MatchingService.cs            — weighted match scoring + Haversine distance
    ├── MongoRepository.cs            — generic get/upsert/delete wrapper per collection
    ├── IMongoRepository.cs           — the interface controllers depend on, so tests can substitute a double
    ├── MongoIndexes.cs               — best-effort index creation at startup
    ├── RequestValidation.cs          — server-side validation shared by the write endpoints
    ├── RoleRequirement.cs            — Farmer/Buyer role check for the create endpoints
    ├── BuildInfo.cs                  — short commit hash of the running build (from RENDER_GIT_COMMIT)
    ├── ClaimsPrincipalExtensions.cs  — Firebase UID from the validated token
    ├── DateOnlySerializer.cs         — DateOnly <-> "yyyy-MM-dd" BSON string
    └── MongoDbOptions.cs             — connection string / database name binding

FromTheFarm.Api.Tests/
├── RequestValidationTests.cs         — 54 tests
├── MatchesControllerTests.cs         — 35 tests
├── UsersControllerTests.cs           — 18 tests
├── ListingsControllerTests.cs        — 23 tests
├── MatchingServiceTests.cs           — 9 tests
├── DemandsControllerTests.cs         — 16 tests
├── AuthControllerTests.cs            — 4 tests
├── BuildInfoTests.cs                 — 5 tests
├── DateOnlySerializerTests.cs        — 3 tests
├── UserProfileTests.cs               — 3 tests
└── TestDoubles.cs                    — in-memory repository and fake caller identity

android/app/src/main/java/com/fromthefarm/app/
├── MainActivity.kt                   — entry point, hosts FarmNavHost
├── data/
│   ├── FarmApi.kt                    — Retrofit interface + REST DTOs (must mirror the API's schema exactly)
│   ├── FarmRepository.kt             — Firebase Google sign-in, ID token, biometric-enabled flag, Retrofit client
│   ├── BuyerRecommendations.kt       — two-section buyer Home feed, distance ordering and deduplication
│   ├── CropNames.kt                  — case and spacing normalization for crop names
│   ├── offline/                      — Room tables, outbox queue, upload logic, background sync and connectivity (see Offline sync)
│   ├── FormValidation.kt             — shared listing/demand field validation
│   ├── UserRole.kt                   — FARMER / BUYER
│   └── SampleData.kt                 — retained for the Part 1 preview screens only; live screens never use it
├── ui/
│   ├── FarmViewModel.kt              — single state holder: session, profile, listings/demands/matches, error mapping
│   ├── AppLanguage.kt                — applies the profile's language as the app language
│   ├── UiText.kt                     — translatable message keys the ViewModel reports and screens resolve
│   ├── navigation/FarmNavHost.kt      — authenticated shell: bottom nav, editors, match detail, biometric lock screen, buyer recommendations, harvest calendar
│   └── screens/
│       ├── LiveForms.kt              — RecordEditor (listing/demand form), NearbyFilter (buyer search), ProfileEditor (onboarding/settings)
│       ├── BiometricAction.kt        — fingerprint/face prompt button
│       ├── ListingPhoto.kt           — photo loader/downscaler + PhotoTools.prepare()
│       └── (Part 1 preview screens kept as design references, not wired into the live shell)
└── src/test/java/com/fromthefarm/app/
    ├── data/FarmApiTest.kt           — request/response shape against MockWebServer
    ├── data/FormValidationTest.kt
    ├── data/BuyerRecommendationsTest.kt
    ├── data/CropNamesTest.kt
    ├── ui/FarmViewModelTest.kt       — session, error handling, save/delete, match lifecycle
    └── ui/SettingsProfileTest.kt
```

## Offline sync

Creating a listing or a demand request works without a connection. The app keeps the record on the phone and uploads it later, and the server guarantees it is stored once however many times the upload is retried.

- Every new record gets a `clientGeneratedId` (a random UUID) on the phone before the first attempt. If the request fails because there is no connection, or the service is asleep and times out, the record is saved in a Room table called the outbox together with that id, and the form closes with a "Saved on this phone" message.
- The outbox uploads on its own. While the app is open, a network callback (Android Developers, n.d.-e) starts an upload as soon as the phone has a working connection. If the app has been closed, a WorkManager job that only runs while a network is available does it, retrying with exponential back-off (Android Developers, n.d.-d). Records go oldest first, one upload at a time.
- A retry can never create a second record. The API looks for an existing record with the same owner and `clientGeneratedId` before creating one and returns it with 200 if found. A unique partial index on (owner, `clientGeneratedId`) in MongoDB is the backstop for two retries that arrive at the same moment; it is partial rather than sparse because a compound sparse index still indexes records that have only one of the two fields.
- Upload outcomes are handled by cause. A dropped connection leaves the record queued and is not counted as an attempt. A 5xx, 408 or 429 response counts an attempt and the record is marked **failed** after five. Any other refusal (validation, ownership) marks it failed immediately with a message, and the next record still uploads. A 401 pauses uploading until the person signs in again, and nothing is lost.
- Reading keeps working when the connection drops during a session. The last listings and demands the server returned are cached in Room per user and shown if a refresh fails. Signing out clears that cached copy; records that have not uploaded yet stay on the phone and upload the next time the same account signs in. Every query is scoped to the signed-in user.
- The screens read sync status from `FarmViewModel.sync`: `offline`, `syncing`, `records` (each with `PENDING` or `FAILED` status and the last error), `pendingCount` and `failedCount`, plus `syncNow()`, `retryRecord(id)` and `discardRecord(id)`. It sits outside `FarmState` so a sign-in or sign-out cannot wipe the queue the person is looking at.
- Limits: only creating is queued, so editing or deleting a record needs a connection. Starting the app from a cold start still needs a connection, because the session and profile are not cached. A queued listing photo is still stored inline with the record, as it is on the server, until photos move to object storage. The Room schema is version 1 with no migration yet; the first change to a table needs one, because the outbox holds unsent data.

The storage, queue and upload rules are covered by 13 unit tests: the upload logic against a mock server (ordering, a dropped connection reusing the same id, refusals, retry limit, a rejected login, per-account scoping, duplicate ids) and the view model (queueing while offline, upload on reconnect, a lost response not duplicating, a refused create not being queued, demands, retry and discard). The Room database itself and the connectivity callback need a device or emulator; `Documentation/Offline-Sync-Demo.md` has the steps to demonstrate and verify them.

Room is the Android-recommended way to keep structured data in a local SQLite database (Android Developers, n.d.-c).

## Localisation

The app is available in **English, isiZulu and Afrikaans**.

- **One setting.** The profile's *Preferred language* (chosen at onboarding and changeable in Settings, then **Save preferences**) is the single language setting. The app follows it, and the API writes push notifications in the same language, so the screens and the notifications agree. On Android 13 and above the language can also be changed from Android's own per-app language settings.
- **Where the text lives.** Every visible string is in `android/app/src/main/res/values/strings.xml` (English), `values-zu/strings.xml` (isiZulu) and `values-af/strings.xml` (Afrikaans): 209 strings in each, with matching keys. The notification channel name and description are in `notification_strings.xml` in each language folder. To add a language, add a `values-xx` folder with the same keys and list it in `res/xml/locales_config.xml` and in `AppLanguage.supported`.
- **How it works.** `AppLanguage` applies the saved profile language with AppCompat's per-app locale support (`MainActivity` is an `AppCompatActivity`, and the theme has an AppCompat parent). The API still sends status values such as *Suggested* and *Confirmed* in English; the app maps them to translated labels. The ViewModel reports messages as `UiText` keys, and the screen resolves them when drawn, so a message always appears in the active language. Form validation returns string resources. Developer-facing setup errors, such as a missing Firebase configuration, are not translated.
- **Status of the translations.** The isiZulu and Afrikaans text is a first draft awaiting a native-speaker review. The checklist and the terms used are in `Documentation/Test-Matrix.md`.
- **Not translated.** The Part 1 preview screens kept as design references are not wired into the app and are still English.

## Testing status

- **Backend**: 170 xUnit test methods, run in CI on every push. Every controller except `HealthController` is tested against an in-memory `IMongoRepository` double: ownership, role and validation rules for listings, demands and profiles; the whole match lifecycle (Suggested → Confirmed → Completed with no stage skipped, contact details hidden until confirmed, one rating per user, outsiders get 404); feed generation from both the farmer's and the buyer's side; and the sign-in profile create-or-fetch. Alongside them sit matching and distance calculations, crop/radius/quantity/freshness rules and BSON date serialization. Not covered: `HealthController`, the concrete `MongoRepository` and `MongoIndexes` (they need a live cluster), and `ClaimsPrincipalExtensions`.
- **Android**: 65 unit tests (all passing) cover the Retrofit contract, Firebase Bearer headers, validation, crop normalization, buyer recommendations, range filtering, recommendation deduplication, catalogue preservation after nearby searches, session recovery, CRUD operations, match lifecycle, settings, the offline queue and upload logic, and notification rules. Compose UI, biometric, image-picker and device-location flows require emulator or physical-device tests.
- **Manual**: `Documentation/Test-Matrix.md` records the manual localisation tests and regression checklist, and says what was and was not run. On an emulator, the sign-in screen, the signed-in screens (as a Farmer and a Buyer, using fake data) and the language switch from Settings were checked in English, isiZulu and Afrikaans. Not yet run: Google sign-in with a real account, the real API, and a physical phone (push delivery, the system fingerprint dialog).

Run the complete local checks with:

```powershell
dotnet test FromTheFarm.Api.Tests
cd android
.\gradlew.bat test assembleDebug lintDebug
```

## Store publication readiness

| Item | Value |
| --- | --- |
| App name | From The Farm |
| Package name | `com.fromthefarm.app` |
| Version | `versionName` 0.1, `versionCode` 1 (set in `android/app/build.gradle.kts`) |
| Minimum SDK | 26 (Android 8.0) |
| Target and compile SDK | 34 (Android 14) |
| Icon | Green rounded square with a leaf, `android/app/src/main/res/drawable/ic_launcher.xml` |
| Languages | English, isiZulu, Afrikaans (see Localisation) |
| Short description | Match smallholder farmers' produce with nearby buyers. |
| Signed release APK | Attached to the GitHub Release created at the final tag: [Releases](https://github.com/Kaehil-I/from-the-farm/releases) |

**Permissions the app declares, and why**

| Permission | Used for |
| --- | --- |
| `INTERNET`, `ACCESS_NETWORK_STATE` | Calling the API, and detecting when the connection returns so saved records upload. |
| `ACCESS_FINE_LOCATION`, `ACCESS_COARSE_LOCATION` | The optional **Use my current location** button on listing, demand and nearby-search forms. Coordinates can be typed instead. |
| `POST_NOTIFICATIONS` | Match notifications on Android 13 and above. It is requested once, after onboarding, and only for people who have notifications switched on. |
| `USE_BIOMETRIC` | The optional biometric lock on the app. |

The app does not declare a camera or storage permission. A listing photo is chosen with the system photo picker, which needs neither.

## Deployment and CI

- The API ships as a container and is available at `https://from-the-farm.onrender.com`. Render builds the service from `FromTheFarm.Api/Dockerfile`. Render supports repository Dockerfiles and requires web services to listen on `0.0.0.0` using the assigned `PORT` value (Render, n.d.-a; Render, n.d.-b). The host health check uses the unauthenticated `GET /api/v1/health`, which also returns the short commit hash of the running build, so the live version can be checked against the repository's latest commit with one request.
- Render redeploys on every push through its GitHub app.
- The only real production secret is `MongoDb__ConnectionString`, set as an environment variable on the host. Collection indexes are created best-effort at startup, so an unreachable cluster doesn't stop the service from starting.
- GitHub Actions: **Backend CI** restores, builds, runs the unit tests and builds the Docker image; **Android CI** runs the unit tests, `assembleDebug` and lint. Each workflow only runs when its own folder changes.

## Known gaps

- Push notifications (Firebase Cloud Messaging) are implemented on both sides: the API registers device tokens and sends a push for new matches and match status changes, and the app registers, asks for the notification permission and opens the right match when a notification is tapped. Delivery on a physical device is not recorded in these docs yet.
- Offline creation and sync covers new listings and demand requests only. Editing and deleting a record needs a connection (see Offline sync).
- The isiZulu and Afrikaans translations have not had a native-speaker review (see Localisation), and the signed-in screens have not yet been checked in those languages on a device.
- Editing a listing can replace its photo but not remove it, and photos are stored inline on the listing document as base64, which is a stopgap for real object storage.
- The rating endpoint is `POST /matches/{id}/rating`; the design document specifies `/ratings` plus a `GET` for the caller's own rating, so the app tracks "already rated" locally.
- The `farmName` / `buyerName` public labels from the design document are not implemented; a match card shows crop, quantity and distance only until the match is confirmed.
- `HealthController`, the concrete `MongoRepository`/`MongoIndexes` and `ClaimsPrincipalExtensions` have no automated tests (see Testing status).
- Buyer Home measures nearby products from the closest open-demand location because the profile does not contain a separate permanent buyer location.
- Render free services may take time to wake after a period of inactivity.

## Team

- Zario Di Paolo — Android frontend (auth wiring, live screens, navigation, forms, biometric unlock), Unit Testing
- Kaehil Indurjeeth — backend deployment, Firebase token verification, matching/feed logic, CI/CD, Unit Testing
- Gregory Luyckfasseel — listing/demand CRUD ownership, Mongo validation, photo support, profile/settings persistence
- Kyra Naidoo — Research Report, unit test drafting, README draft; Part 3: localisation (English, isiZulu, Afrikaans), manual test matrix and translation checklist, README and release notes

## References

Android Developers. n.d.-a. *Jetpack Compose UI app development toolkit*. Available at: https://developer.android.com/compose (Accessed: 22 September 2026).

Android Developers. n.d.-b. *Show a biometric authentication dialog*. Available at: https://developer.android.com/identity/sign-in/biometric-auth (Accessed: 22 September 2026).

Android Developers. n.d.-c. *Save data in a local database using Room*. Available at: https://developer.android.com/training/data-storage/room (Accessed: 5 November 2026).

Android Developers. n.d.-d. *Getting started with WorkManager*. Available at: https://developer.android.com/develop/background-work/background-tasks/persistent/getting-started (Accessed: 5 November 2026).

Android Developers. n.d.-e. *Read network state*. Available at: https://developer.android.com/training/basics/network-ops/reading-network-state (Accessed: 5 November 2026).

Google Firebase. n.d.-a. *Authenticate with Google on Android*. Available at: https://firebase.google.com/docs/auth/android/google-signin (Accessed: 22 September 2026).

Google Firebase. n.d.-b. *Firebase Authentication*. Available at: https://firebase.google.com/docs/auth/ (Accessed: 22 September 2026).

Microsoft. 2026. *Tutorial: Create a controller-based web API with ASP.NET Core*. Available at: https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api (Accessed: 22 September 2026).

MongoDB. n.d.-a. *Connect to an Atlas cluster*. Available at: https://www.mongodb.com/docs/atlas/connect-to-database-deployment/ (Accessed: 22 September 2026).

MongoDB. n.d.-b. *Configure database users*. Available at: https://www.mongodb.com/docs/atlas/security-add-mongodb-users/ (Accessed: 22 September 2026).

Render. n.d.-a. *Docker on Render*. Available at: https://render.com/docs/docker (Accessed: 22 September 2026).

Render. n.d.-b. *Web services*. Available at: https://render.com/docs/web-services (Accessed: 22 September 2026).
