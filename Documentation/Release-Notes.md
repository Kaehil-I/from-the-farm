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

## Localisation (Kyra Naidoo)

- **Three languages.** The whole app is available in English, isiZulu and Afrikaans. Text is in Android string resources (`values`, `values-zu`, `values-af`): 191 strings plus the 2 notification channel strings in each language.
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
- **Not yet verified on a signed-in device.** The sign-in screen and its error message were checked in all three languages on an emulator. Settings, Home, Listings, Calendar, match details, validation and action messages in the three languages still need a device run, because Google sign-in needs the build's signing key to be registered in the Firebase project. See `Documentation/Test-Matrix.md`.
- **Push delivery and biometrics on hardware** have not been demonstrated on a physical device in these notes.
- **Offline scope.** Only creating is queued. Editing and deleting need a connection, and a cold start without a connection cannot load the profile.
- **Photos** are still stored inline on the record, not in object storage.
- **Unit tests only on this branch.** All 65 Android unit tests pass at commit `58ade8f`. The Part 1 preview screens are not wired into the app and remain English.
