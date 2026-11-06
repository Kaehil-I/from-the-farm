# Release notes: Part 3, changes since Part 2

These notes cover everything added after the Part 2 integration (pull request #8, `Integration-Part2`). They are written from the commit history on `Part-3-Integration` and `kyra-localisation-p3`, and say who did what. They describe what the code does; anything that has not been tested on a device is marked in **Known limitations and verification**.

## Summary

| Area | Part 2 | Now |
| --- | --- | --- |
| Offline use | Creating a record needed a connection | New listings and demands can be created offline and upload automatically, exactly once |
| Notifications | None | Push notifications for new matches and match status changes, in the recipient's language |
| Biometric unlock | Strong biometrics only | Android-supported fingerprint and face unlock, with clearer errors |
| Language | The selector saved a preference but every screen stayed English | English, isiZulu and Afrikaans across the app |
| Matching | Generated when the feed was opened | Also generated when a listing or demand is posted, so the other party is alerted at once |
| Photos | Stored inside each listing document | Stored in blob storage (MongoDB GridFS) and served by link |
| API contract | Rating `POST` only; no calendar endpoint | Adds `GET` rating and `GET /calendar` from the design document |

## Photo storage and design-document endpoints (Kaehil Indurjeeth)

- **Blob storage.** Listing photos are no longer base64 text inside the listing document. The API validates the upload, stores the bytes in a MongoDB GridFS bucket and keeps only a link on the listing. `GET /api/v1/photos/{id}` serves the image. The endpoint is public because the app loads images with a plain HTTPS request, and the ids are random 32-character values, not guessable ObjectIds.
- **Cleanup.** Replacing a photo or deleting a listing removes the stored image. Listings created earlier keep their inline image until the photo is replaced.
- **Rating lookup.** `GET /api/v1/matches/{id}/rating` returns the caller's own rating, or 204 if they have not rated. It never exposes the other party's rating.
- **Harvest calendar.** `GET /api/v1/calendar?month=&year=` groups active listings by harvest date, returning crop, quantity, unit and an `isMine` flag, with no coordinates or owner ids.
- **Matching and privacy hardening.** Stale, self-referential and expired matches are no longer created or notified, and browsing shows locations rounded to about 1 km with owner ids removed.
- **Release pipeline.** A signed release workflow builds the APK and app bundle from a tag and publishes them with checksums; keystore and passwords come from repository secrets.

## Offline creation and sync (Gregory Luyckfasseel)

- **Duplicate-proof creates.** A new listing or demand carries a `clientGeneratedId`. If the API already has a record with that id from the same user, it returns the original instead of creating a second one. Unique partial indexes in MongoDB back this up.
- **Room storage and an outbox.** Listings and demands the server returned are cached on the phone per user, so lists still show when a refresh fails. Creates that could not be sent are kept in an outbox.
- **Automatic upload.** Records upload oldest first as soon as the phone has a connection, using a network callback while the app is open and a WorkManager job when it is closed. Dropped connections keep a record queued; refused records are marked failed with a message.
- **Sync state for the screens.** The ViewModel exposes pending, failed and syncing status with `syncNow()`, `retryRecord()` and `discardRecord()`.
- **Documentation and tests.** `Documentation/Offline-Sync-Demo.md` gives the steps to demonstrate it, and the upload logic and ViewModel behaviour are covered by unit tests.

## Push notifications and match alerts (Kaehil Indurjeeth)

- **Device tokens.** The API can register and unregister FCM device tokens on a user's profile.
- **Notifications.** The API sends a push for a new match and for meaningful match status changes (confirmed, completed). The title and body are written in the language saved on the recipient's profile (English, isiZulu or Afrikaans).
- **Matching on create.** Posting a listing or demand now generates matches immediately, so the other party is alerted at once.
- **Android.** The app registers for push, asks for the notification permission once after onboarding for people who have notifications on, handles a revoked permission when posting, and opens the right match when a notification is tapped.
- **CI.** Pull request checks now also run for `Part-3-Integration`.

## Biometric unlock (Zario Di Paolo)

- Biometric unlock now accepts Android-supported fingerprint and face authentication rather than strong biometrics only.
- Hardware and enrolment problems show clearer messages.
- Remaining Compose compile warnings in the main UI flows were removed.
- Offline sync status and saved-record screens (pull request #13): a banner shows the offline, uploading, waiting and failed states with a **Sync now** button, and records saved on the phone are listed on the Listings or Demand screen with **Retry** and **Discard** options and a confirmation before discarding.

## Localisation (Kyra Naidoo)

- **Three languages.** The whole app is available in English, isiZulu and Afrikaans. Text is in Android string resources (`values`, `values-zu`, `values-af`): 209 strings plus the 2 notification channel strings in each language.
- **One language setting.** The profile's preferred language is the single setting. Saving it changes the app language, the app follows it on a new install, and the API writes push notifications in the same language, so the screens and the notifications never disagree. The separate in-app picker that was first built was removed for that reason.
- **Per-app language support.** The app uses Android's per-app language mechanism, which AppCompat provides back to API 26 (the minimum SDK); it has only been run on an API 37 emulator so far. `MainActivity` now extends `AppCompatActivity`, the theme has an AppCompat parent, and a `locales_config.xml` lists the three languages, so on Android 13 and above the language can also be changed from Android's own settings.
- **Everything the user reads is translatable.** Tabs, dialogs, forms, location and photo prompts, biometric prompts and errors, status labels (the API still sends them in English), and the notification channel name and description.
- **Messages and validation.** Errors and confirmations from the ViewModel are now translatable keys (`UiText`) that the screen turns into text when it is drawn, so a message follows the language active at that moment. Form validation returns string resources. Developer-facing setup errors, such as a missing Firebase configuration, are deliberately left as written.
- **Tests.** Five assertions in `FarmViewModelTest`, `OfflineQueueTest` and `SettingsProfileTest` that matched English wording now check the message key. All 65 Android unit tests pass.
- **Documentation.** `Documentation/Test-Matrix.md` holds the manual test matrix, regression checklist and translation checklist; the README has a Localisation section.

## Documentation

- The README moved to the repository root and gained the video walkthrough and a note on where active development lives (Kaehil Indurjeeth).
- Backend test coverage was restored in the README after the Part 2 rewrite (Kaehil Indurjeeth).
- Offline sync, localisation, testing status and known gaps were added or updated for Part 3.

## Known limitations and verification

- **Translations need a native-speaker review.** The isiZulu and Afrikaans text is a first draft. The push notification wording written by the API has also not been reviewed.
- **Tested on an emulator with fake data, not with a real account.** The real screens and ViewModel were run in English, isiZulu and Afrikaans as both a Farmer and a Buyer (Home, Listings or Demand, forms, Calendar, Settings, match details, the locked screen) and the language switch from Settings was tested end to end. All 5 of those tests pass. Google sign-in, the real API and a real phone were not used, because sign-in needs the build's signing key to be registered in the Firebase project. Details and the rows still not run are in `Documentation/Test-Matrix.md`.
- **Confirmation messages vanish quickly.** "Preferences saved." and "Exchange marked completed." were not seen on screen within 3 seconds, most likely because the automatic refresh that follows clears them. This predates the localisation work (Test-Matrix finding F-01).
- **Physical-device checks.** Push delivery and biometric unlock were checked on a physical phone by Kaehil. The Android notification settings screen has not been.
- **Offline scope.** Only creating is queued. Editing and deleting need a connection, and a cold start without a connection cannot load the profile.
- **Failed-upload error text is still English.** The reason shown on a saved record that failed to upload is written by the upload code (`OutboxProcessor`) and stored with the record, so it is not translated yet. The rest of the offline sync screen is translated.
- **Photos from before the blob-storage change** keep their inline image until the photo is replaced.
- **Unit tests only on this branch.** All 65 Android unit tests pass at commit `58ade8f`. The Part 1 preview screens are not wired into the app and remain English.
