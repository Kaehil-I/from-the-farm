# Push notifications: how they work, how to set them up

Part 3 sends a push notification to the other person when a match is created or its status changes. This page covers the behaviour, the configuration and what to check when a notification does not arrive. The step-by-step recording script is in [Notification-Demo.md](Notification-Demo.md).

## What triggers a notification

| Event | Who is notified | Title (English) | When it fires |
| --- | --- | --- | --- |
| New match | The party who did **not** cause it | New match | A listing or demand is posted or edited and pairs with an existing record, or a feed fetch finds a pair for the first time |
| Match confirmed | The other party | Match confirmed | One party calls `POST /matches/{id}/confirm` |
| Match completed | The other party | Match completed | One party calls `POST /matches/{id}/complete` |

Rules that apply to every notification:

- A user who switched notifications off in Settings receives nothing. This is enforced on the server, so it holds whatever build is on the phone.
- The wording is written in the recipient's saved language (English, isiZulu or Afrikaans), not the sender's.
- A match produces **one** "new match" alert. Reopening the feed regenerates matches but only alerts for ones that did not exist before.
- Sending is best effort. If Firebase is unreachable the request that caused it (a confirm, a new listing) still succeeds and a warning is logged.
- Only suggestions that are still valid are created: the listing must be Active, the demand Open and not past its deadline, the two records must belong to different users and the crop must match. See `MatchGenerationService.IsStillAvailable`.

## How a notification travels

1. After sign-in the app reads its Firebase Cloud Messaging (FCM) token and registers it with `PUT /api/v1/users/me/devices`. This repeats on every launch and whenever Firebase rotates the token.
2. The token is stored on the user's profile (up to 10 per user; the least recently seen is dropped). It is never returned by any endpoint.
3. When an event happens, `NotificationService` checks the recipient's preference, builds the message in their language and calls `FcmPushSender`, which uses the Firebase Admin SDK (HTTP v1 API).
4. FCM delivers it. If the app is in the background Android draws the notification itself on the `matches` channel; if the app is open, `FarmMessagingService` draws it.
5. Tapping it opens the app and then the match, using the `type` and `matchId` values in the push data.
6. On sign-out the app unregisters the token (`DELETE /api/v1/users/me/devices`) and deletes it, so the phone stops receiving that account's alerts.

A token identifies a phone, not a person. If a second account signs in on the same phone, the token moves to the new account and the earlier account stops receiving alerts on it.

## Endpoints

| Method and path | Body | Result |
| --- | --- | --- |
| `PUT /api/v1/users/me/devices` | `{ "token": "..." }` | `204`. Adds the token or refreshes when it was last seen. `400` for an empty, oversized or whitespace-containing token; `404` if no session exists yet. |
| `DELETE /api/v1/users/me/devices` | `{ "token": "..." }` | `204`, including when the token was not held. The token travels in the body to keep it out of request logs. |

## Push data

Every push carries a `data` payload the app reads when it is tapped:

| Key | Values |
| --- | --- |
| `type` | `match_new`, `match_confirmed`, `match_completed` |
| `matchId` | The match id, for example `listing-id:demand-id` |

## Configuration

The API needs a Firebase **service account key** to send. Without one it still starts and everything else works, but pushes are off.

**On Render**

1. Firebase console, `fromthefarm-poe`, Project settings, Service accounts, **Generate new private key**.
2. Render, the `from-the-farm` service, **Environment**, **Secret Files**: add `firebase-service-account.json` and paste the JSON as its contents.
3. **Environment Variables**: add `Firebase__ServiceAccountPath` = `/etc/secrets/firebase-service-account.json` (two underscores).
4. Save and deploy. The startup log must contain `Push notifications enabled.`. If it says `disabled (no Firebase service account configured)` the path or file name does not match.

**Locally:** keep the key outside the repository and set `GOOGLE_APPLICATION_CREDENTIALS` to its path (or `Firebase:ServiceAccountPath` in user secrets).

The key can send notifications for the whole Firebase project. Never commit it, paste it into chat or leave a copy in a synced folder. If it leaks, delete the key in the Firebase console and generate another.

## On the phone

- Requires Google Play services (the standard emulator images and real phones have it).
- Android 13 and later asks for the notification permission once, after onboarding, and only if "Save notification preference" is on. If it was refused, enable it under the phone's Settings, Apps, From The Farm, Notifications.
- The channel is named "Match updates" (`matches`). The code lives in `app/.../notifications/`.

## Troubleshooting

| Symptom | Likely cause |
| --- | --- |
| No notification at all | Permission refused; notifications off in the recipient's Settings; the recipient never opened the app after signing in (no token registered); the two records do not match (crop, radius, expired demand). |
| Log says `Push notifications disabled` | Secret file or `Firebase__ServiceAccountPath` missing or mismatched on Render. |
| Log shows `Could not send a ... notification` | Firebase rejected or could not be reached. The warning includes the exception. Check the service account still exists and the FCM API is enabled for the project. |
| First notification is slow | Render's free tier sleeps. The first request after idle wakes it. |
| Works on one phone, not another | That phone has no Play services, or the account on it signed out (token removed). |
| A tapped notification only opens the home screen | The match no longer exists (the listing was deleted or the demand expired), or the app was still locked behind biometrics; unlock and open Matches. |

## Tests

- Backend: `DeviceTokenTests` (registration), the notification section of `MatchesControllerTests`, `CreationMatchingTests` and `MatchHardeningTests`.
- Android: `NotificationRulesTest`, plus the registration and tap-through cases in `FarmViewModelTest` and `FarmApiTest`.
- Not covered by automated tests: actual delivery through FCM. That is verified by hand with the script in [Notification-Demo.md](Notification-Demo.md).
