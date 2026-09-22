# From The Farm

**From The Farm** matches South African smallholder farmers' produce listings with nearby buyer demand requests, scored by crop match, distance, quantity fit and harvest timing. The project has two parts:

- **`FromTheFarm.Api`** — ASP.NET Core 8 Web API, MongoDB Atlas, Firebase ID token authentication.
- **`android`** — Kotlin + Jetpack Compose app. Google sign-in via Firebase, live CRUD against the API, a scored match feed, and optional biometric device unlock.

Part 1's UI-only prototype (mock data, no networking) has been replaced end to end: both sides are now live and talk to each other over a real REST contract. Jetpack Compose is Android's recommended modern toolkit for native, state-driven user interfaces (Android Developers, n.d.-a), while ASP.NET Core supplies the controller, routing and dependency-injection structure used by the API (Microsoft, 2026).

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
- **Farmers** manage produce listings (crop, quantity, harvest date, location, optional photo). Photos are selected from the device and downscaled before upload.
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
│   ├── UsersController.cs            — GET/PUT users/me (profile + onboarding)
│   ├── ListingsController.cs         — farmer listings CRUD, buyer distance search
│   ├── DemandsController.cs          — buyer demand requests CRUD
│   ├── MatchesController.cs          — scored feed, detail, confirm/complete/rate
│   └── HealthController.cs           — unauthenticated Mongo ping
├── Models/                           — Listing, DemandRequest, MatchDocument, Rating, UserProfile (has OnboardingComplete), GeoLocation
└── Services/
    ├── MatchingService.cs            — weighted match scoring + Haversine distance
    ├── MongoRepository.cs            — generic get/upsert/delete wrapper per collection
    ├── ClaimsPrincipalExtensions.cs  — Firebase UID from the validated token
    ├── DateOnlySerializer.cs         — DateOnly <-> "yyyy-MM-dd" BSON string
    └── MongoDbOptions.cs             — connection string / database name binding

FromTheFarm.Api.Tests/
├── MatchingServiceTests.cs           — 9 tests
├── DateOnlySerializerTests.cs        — 3 tests
└── UserProfileTests.cs               — 3 tests

android/app/src/main/java/com/fromthefarm/app/
├── MainActivity.kt                   — entry point, hosts FarmNavHost
├── data/
│   ├── FarmApi.kt                    — Retrofit interface + REST DTOs (must mirror the API's schema exactly)
│   ├── FarmRepository.kt             — Firebase Google sign-in, ID token, biometric-enabled flag, Retrofit client
│   ├── BuyerRecommendations.kt       — two-section buyer Home feed, distance ordering and deduplication
│   ├── CropNames.kt                  — case and spacing normalization for crop names
│   ├── FormValidation.kt             — shared listing/demand field validation
│   ├── UserRole.kt                   — FARMER / BUYER
│   └── SampleData.kt                 — retained for the Part 1 preview screens only; live screens never use it
├── ui/
│   ├── FarmViewModel.kt              — single state holder: session, profile, listings/demands/matches, error mapping
│   ├── navigation/FarmNavHost.kt      — authenticated shell: bottom nav, editors, match detail, biometric lock screen
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

## Testing status

- **Backend**: 14 xUnit test methods cover matching and distance calculations, crop/radius/quantity/freshness rules, BSON date serialization and onboarding. They run in CI on every push. The controllers and `MongoRepository` still need broader integration coverage.
- **Android**: 36 unit tests cover the Retrofit contract, Firebase Bearer headers, validation, crop normalization, buyer recommendations, range filtering, recommendation deduplication, catalogue preservation after nearby searches, session recovery, CRUD operations, match lifecycle and settings. Compose UI, biometric, image-picker and device-location flows require emulator or physical-device tests.

Run the complete local checks with:

```powershell
dotnet test FromTheFarm.Api.Tests
cd android
.\gradlew.bat test assembleDebug lintDebug
```

## Deployment and CI

- The API ships as a container and is available at `https://from-the-farm.onrender.com`. Render builds the service from `FromTheFarm.Api/Dockerfile`. Render supports repository Dockerfiles and requires web services to listen on `0.0.0.0` using the assigned `PORT` value (Render, n.d.-a; Render, n.d.-b). The host health check uses the unauthenticated `GET /api/v1/health`.
- The only real production secret is `MongoDb__ConnectionString`, set as an environment variable on the host.
- GitHub Actions: **Backend CI** restores, builds, runs the unit tests and builds the Docker image; **Android CI** runs the unit tests, `assembleDebug` and lint. Each workflow only runs when its own folder changes.

## Known gaps

- Push notifications (Firebase Cloud Messaging) are not implemented on either side.
- Offline creation and sync (Room) is not implemented. The API already accepts a `clientGeneratedId` for it.
- The language selector (English / isiZulu / Afrikaans) saves to the profile, but the app's text is not translated — every screen is English regardless of the choice.
- Editing a listing does not replace its photo (`PUT /listings/{id}` ignores `photoBase64`), and photos are stored inline on the listing document as base64, which is a stopgap for real object storage.
- The rating endpoint is `POST /matches/{id}/rating`; the design document specifies `/ratings` plus a `GET` for the caller's own rating, so the app tracks "already rated" locally.
- The `farmName` / `buyerName` public labels from the design document are not implemented; a match card shows crop, quantity and distance only until the match is confirmed.
- Backend controller and full match-lifecycle integration coverage remains limited (see Testing status).
- Buyer Home measures nearby products from the closest open-demand location because the profile does not contain a separate permanent buyer location.
- Render free services may take time to wake after a period of inactivity.

## Team

- Zario Di Paolo — Android frontend (auth wiring, live screens, navigation, forms, biometric unlock)
- Kaehil Indurjeeth — backend deployment, Firebase token verification, matching/feed logic, CI/CD
- Gregory Luyckfasseel — listing/demand CRUD ownership, Mongo validation, photo support, profile/settings persistence
- Kyra Naidoo — Research Report, unit test coordination, README

## References

Android Developers. n.d.-a. *Jetpack Compose UI app development toolkit*. Available at: https://developer.android.com/compose (Accessed: 22 September 2026).

Android Developers. n.d.-b. *Show a biometric authentication dialog*. Available at: https://developer.android.com/identity/sign-in/biometric-auth (Accessed: 22 September 2026).

Google Firebase. n.d.-a. *Authenticate with Google on Android*. Available at: https://firebase.google.com/docs/auth/android/google-signin (Accessed: 22 September 2026).

Google Firebase. n.d.-b. *Firebase Authentication*. Available at: https://firebase.google.com/docs/auth/ (Accessed: 22 September 2026).

Microsoft. 2026. *Tutorial: Create a controller-based web API with ASP.NET Core*. Available at: https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api (Accessed: 22 September 2026).

MongoDB. n.d.-a. *Connect to an Atlas cluster*. Available at: https://www.mongodb.com/docs/atlas/connect-to-database-deployment/ (Accessed: 22 September 2026).

MongoDB. n.d.-b. *Configure database users*. Available at: https://www.mongodb.com/docs/atlas/security-add-mongodb-users/ (Accessed: 22 September 2026).

Render. n.d.-a. *Docker on Render*. Available at: https://render.com/docs/docker (Accessed: 22 September 2026).

Render. n.d.-b. *Web services*. Available at: https://render.com/docs/web-services (Accessed: 22 September 2026).
