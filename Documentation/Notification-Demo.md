# Push notifications: how to demonstrate and verify them

This is the evidence for the Part 3 notification requirement: a push delivered to a second device when a match is created and when its status changes. Background and troubleshooting are in [Push-Notifications.md](Push-Notifications.md).

## What you need

- Two devices with Google Play services: a real phone and a second phone or an emulator. Call them **Farmer device** and **Buyer device**.
- Two different Google accounts that can sign in to the app (their debug or release certificate fingerprints must be registered in the Firebase project).
- The same build on both, built from the latest `Part-3-Integration` or the signed release APK.
- Screen recording on the Buyer device (Android's built-in recorder is enough), and a second window or screen with the Render logs open.

## Before you start

1. Open `https://from-the-farm.onrender.com/api/v1/health`. It wakes the API and shows the deployed commit; confirm it matches the commit you are demonstrating.
2. In the Render logs confirm the last start-up printed `Push notifications enabled.`.
3. Sign in on both devices and finish onboarding: one as **Farmer**, one as **Buyer**. Accept the notification permission when asked and leave "Save notification preference" on.
4. Use a crop and a place close to each other (for example Tomatoes, both near Durban) and a demand deadline in the future.

## The demonstration

| Step | Action | Expected result | Capture |
| --- | --- | --- | --- |
| 1 | Buyer device: **Demand** tab, **Post demand** for Tomatoes, then lock the screen (or press Home). | The demand saves. | Screenshot |
| 2 | Farmer device: **Listings** tab, **Add listing** for Tomatoes, **Save**. | The listing saves. | Screenshot |
| 3 | Buyer device, within a few seconds. | A **New match** notification appears on the lock screen: "A new Tomatoes match is waiting for you." | **Screen recording** |
| 4 | Buyer device: tap the notification. | The app opens on that match's detail screen. | Recording |
| 5 | Buyer device: tap **Confirm** (put the Farmer device in the background first). | The match shows Confirmed. | Screenshot |
| 6 | Farmer device. | A **Match confirmed** notification appears. Tapping it opens the match, now showing the buyer's contact details. | Recording |
| 7 | Farmer device: mark the match **Complete**. | The match shows Completed. | Screenshot |
| 8 | Buyer device. | A **Match completed** notification appears, and the rating option is available. | Recording |

## Extra cases worth showing

| Case | How | Expected |
| --- | --- | --- |
| Opt out | Buyer device: Settings, untick "Save notification preference", Save. Farmer posts another Tomatoes listing. | No notification reaches the Buyer. |
| Open app | Keep the Buyer app in the foreground while the Farmer posts a listing. | The notification still appears. |
| Language | Set the Buyer's language to isiZulu or Afrikaans in Settings, then repeat step 2. | The notification text is in that language. |
| Sign out | Buyer device: sign out, then the Farmer posts a listing. | Nothing arrives on that phone. |
| No stale matches | Farmer deletes the listing before the Buyer confirms. | The suggestion disappears from the Buyer's feed. |

## What to capture for the evidence pack

- One continuous recording of steps 1 to 4 showing the lock screen notification and the tap opening the match.
- The Render log lines `Push notifications enabled.` and the deployed commit from `/health`.
- A screenshot of the Actions tab with the latest run green.
- The notes of which device was which, the build commit and the date.

## If nothing arrives

Work down this list; most failures are one of the first three.

1. Did the Buyer open the app at least once after signing in? That is when the phone registers.
2. Is the notification permission granted (phone Settings, Apps, From The Farm, Notifications)?
3. Does the Render log say `enabled`? If it says `disabled`, the secret file is not configured.
4. Do the records really match: same crop, within the search radius, demand deadline not passed?
5. Search the Render logs for `Could not send`. The line includes the reason.

## Notes for presenting

- The first request after a quiet period is slow because Render's free tier is asleep. Wake it with the health request before you start.
- Delivery normally takes one to three seconds. A delay of ten seconds or more on an emulator is common.
- Do not show the Firebase service account file or the Render secret file contents on screen.
