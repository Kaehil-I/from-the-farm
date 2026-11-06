# Submission pack: PROG7314 POE (From The Farm)

This page maps the POE brief and marking rubric to the evidence in this repository, so a marker can find each item quickly.

## Links

| Item | Where |
| --- | --- |
| Submitted repository | https://github.com/EMKNDN/emkndn-prog7314-2026-prog7314-poe-kaehil-i |
| Final commit tag | `Final-POE` (Tags page of the repository) |
| Signed release APK, app bundle and checksums | Releases page of the repository, created from the `Final-POE` tag |
| Demo video (unlisted YouTube) | https://youtu.be/MoI_sfi8ccc, also at the top of the [README](../README.md) |
| Live API | https://from-the-farm.onrender.com/api/v1/health |
| Development repository (pull requests, reviews, CI history) | https://github.com/Kaehil-I/from-the-farm |

## Brief checklist

| Brief requirement | Evidence |
| --- | --- |
| Kotlin source code in the repository, no zip files | `FromTheFarm/android` (Kotlin, Jetpack Compose) and `FromTheFarm/FromTheFarm.Api` (ASP.NET Core). Everything was pushed through git. |
| Professional comments and referencing in code | Comments explain intent and cite sources where a pattern or API was adopted, for example the GridFS store, WorkManager scheduling and FCM sender. |
| System logging | Android `Log` calls: activity lifecycle in `MainActivity`, push events in `FarmMessagingService`, background sync in `SyncWorker`, and sign-in, lock, sync and match state changes through the view model's logger. Tokens and personal data are not logged. |
| Technical README with the video link | [README](../README.md): purpose, architecture, how to run, offline sync, localisation, testing, store readiness, CI, known gaps, team, references. |
| Demo video with voice-over | See the link above. The planned sequence is in [Video-Running-Order.md](Video-Running-Order.md). |
| Formal release notes | [Release-Notes.md](Release-Notes.md): every change since the prototype, by team member. |
| Store publication readiness | README section "Store publication readiness", signed APK and bundle on the Releases page, and [screenshots](screenshots/). |
| AI write-up | [AI-Use-Statement.md](AI-Use-Statement.md). |
| Final commit tagged as the Final POE | Tag `Final-POE`. |
| GitHub Actions builds and runs tests on pushes | `.github/workflows/`: Backend CI, Android CI and Release. Run history is on both repositories. |

## Rubric, 100 marks

| Criterion | Marks | Evidence | Honest notes |
| --- | --- | --- | --- |
| App runs on a mobile device | 5 | The demo video records the app on a physical phone. | The release build is the signed APK from the Release workflow. |
| Biometric authentication | 10 | Optional biometric lock, enabled in Settings, applied when the app is reopened. Code in `BiometricAction.kt`, `LoginScreen.kt` and `SettingsScreen.kt`. Checked on a physical phone. | It is a local per-device lock on top of the saved session, and not a separate account. |
| Offline mode with sync | 10 | Room storage, outbox queue, WorkManager and connectivity-triggered upload, with `clientGeneratedId` making retries duplicate-safe. Steps in [Offline-Sync-Demo.md](Offline-Sync-Demo.md). | Only creating listings and demands is queued. Editing and deleting need a connection. |
| Real-time notifications | 10 | Firebase Cloud Messaging end to end: device token registration, server-side sending for new matches and status changes in the recipient's language, Android permission and tap-through to the match. See [Push-Notifications.md](Push-Notifications.md) and [Notification-Demo.md](Notification-Demo.md). | Delivery is checked by hand, not by automated tests. |
| Multi-language | 10 | English, isiZulu and Afrikaans across the app and the push text. See the Localisation section of the README and [Test-Matrix.md](Test-Matrix.md). | The isiZulu and Afrikaans text has not had a native-speaker review. |
| User-defined feature 4 | 10 | Proximity match feed: four-factor score (distance, crop, quantity, freshness), computed on the server. | |
| User-defined feature 5 | 10 | Harvest calendar, produce listing manager with photos, buyer demand board and trust rating are the other features from the Part 1 design. | Confirm with the lecturer which two are marked. All are in the video. |
| User interface | 10 | Material 3 and Jetpack Compose, with form validation and offline and sync status shown on the screens. Screenshots in [screenshots/](screenshots/). | |
| Prepared for the Play Store | 5 | README store-readiness table (name, package, version, SDK levels, icon, permissions), signed APK and bundle, and promotional screenshots. | The app is not published to Google Play, which the brief does not require. |
| AI-use write-up | 5 | [AI-Use-Statement.md](AI-Use-Statement.md). | |
| GitHub, README and automated testing | 10 | 140+ commits from four contributors, pull-request reviews on the development repository, 236 backend tests and 65 Android unit tests, CI on every change. | |
| Demo video | 5 | See the link above. | |

## Other learning outcomes in the brief

| Outcome | Evidence |
| --- | --- |
| Use blob storage | Listing photos are stored in MongoDB GridFS and served from `GET /api/v1/photos/{id}`. |
| Use a NoSQL database to store and read data | MongoDB Atlas: users, listings, demands, matches and ratings, plus the photo bucket. |
| Prepare an app for publication in the Play Store | See "Store publication readiness" above. |

## Verifying the release

1. Open the Releases page and download `FromTheFarm-<version>.apk` and `SHA256SUMS.txt`.
2. Check the checksum matches. On Windows: `Get-FileHash FromTheFarm-<version>.apk`.
3. The release workflow log prints the signing certificate fingerprint. The release key's SHA-1 is registered in the Firebase project so Google sign-in works on the signed build.

## Known gaps

The README "Known gaps" section is the full list. The main ones:
- the translations have not had a native-speaker review
- only creating records is queued offline
- the app builds its calendar from the listings it already holds and does not call the new `/calendar` endpoint
- the app does not yet call the rating `GET`
