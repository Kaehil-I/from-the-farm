# Final demo video: running order

One video that covers every point the POE brief lists, in an order that needs only one physical phone plus one second device. The scripts for the notification and offline segments are in [Notification-Demo.md](Notification-Demo.md) and [Offline-Sync-Demo.md](Offline-Sync-Demo.md); this page puts them in one sequence. Target length is 10 to 12 minutes, recorded with a voice-over.

> **Recording status (6 November 2026):** this remains the final-video plan. Before submission, confirm the recording was completed against the signed release commit/tag, upload it, replace or confirm the README video URL, and record the link in `Final-Release-Checklist.md`. The existing README link must not be assumed to be the Part 3 evidence without that confirmation.

## The brief's checklist and where each item is covered

| Required in the video | Segment |
| --- | --- |
| Registering and logging in with SSO | 1 |
| Biometric verification | 1 |
| Changing and saving settings | 2 |
| Real-time communication with the hosted REST API | 3, 5 |
| Offline action, then synchronisation on reconnect | 7 |
| Visual confirmation in the hosted auth service, API and database | 2, 3, 4, 7, 8 |
| User-defined features from the Part 1 design | 3, 4, 5, 6 |
| Push notifications (Firebase Cloud Messaging) | 4, 5 |
| Multi-language support | 2 |
| Blob storage and NoSQL read and write | 4 |
| Play Store readiness | 9 |

## Setup

**Devices**
- **Phone (physical, signed release APK).** This is the main device and the **Buyer** for the whole video. It is the one that receives the push notifications.
- **Second device (a teammate's phone, or an emulator with Google Play services running the debug build).** This is the **Farmer** and acts only in segments 4 and 5. Say in the voice-over that it is standing in for a second user.

**Accounts.** Two Google accounts, one per role. Sign the Buyer in on the phone before recording only if you want to skip the first-run screens; the sign-in itself is segment 1, so it is better to start signed out.

**On your laptop, in separate windows**
- Firebase console, Authentication, Users.
- Atlas, Browse Collections for the database, with `Users`, `Demands`, `Listings`, `Matches`, `photos.files`.
- The Render service Logs tab.
- The GitHub Actions page and the Releases page.
- `https://from-the-farm.onrender.com/api/v1/health` (open it before recording to wake the free-tier server).

**Before pressing record**
1. Wake the API with the health URL and confirm the commit matches the release.
2. Check Render's log says `Push notifications enabled.`.
3. Clear old test data from the two accounts' listings and demands, or you may get matches you did not plan.
4. Settle the data for the shot: Buyer demand for **Tomatoes**, Farmer listing for **Tomatoes**, both near each other, demand deadline in the future.
5. Turn on Do Not Disturb exceptions so the notification appears, but turn off other apps' notifications so none leaks into the video.
6. Do one full dry run. Most retakes come from a missing token registration or a mismatched crop.

## Running order

| # | Time | On screen | What to say | Check |
| --- | --- | --- | --- | --- |
| 0 | 0:00 | Title card or the repo README, then the phone's home screen with the app icon. | The team, the problem (small farmers reaching local buyers), and a one-line architecture: Android app, ASP.NET API on Render, MongoDB Atlas, Firebase for sign-in and push. | App icon and name visible. |
| 1 | 0:45 | Phone, signed out. Tap **Sign in with Google**, choose the Buyer account, complete onboarding (role Buyer, language, search radius). Then **Settings**, turn on the biometric lock, close the app and reopen it. Unlock with the fingerprint. | SSO is Firebase Authentication with Google. Every API call carries the Firebase token and the server verifies it. Biometrics lock access on this device. | Laptop: Firebase Authentication, Users shows the new account. The Atlas `Users` document exists with role Buyer. |
| 2 | 2:15 | **Settings**: change the language to isiZulu, change the radius, leave the notification toggle on, save. Show a screen in isiZulu, then switch to Afrikaans, then back to English. | Settings are saved to the API per user. The app is translated into English, isiZulu and Afrikaans. | Atlas `Users` document shows the new language and radius after a refresh. |
| 3 | 3:00 | Buyer: **Demand** tab, post a demand for Tomatoes. Show it in the list. | Creating a demand is a round trip to the REST API; the server validates it and stores it. | Atlas `Demands` shows the document. Render logs show the request. |
| 4 | 3:45 | Put the phone on the lock screen. On the second device, sign in as the Farmer and **Add listing** for Tomatoes **with a photo**. Cut to the phone: the **New match** notification appears. Tap it. | A new listing triggers matching on the server; the buyer is notified through Firebase Cloud Messaging in their saved language. Photos are stored in blob storage and the listing keeps a link. | The notification is on the lock screen, and tapping it opens the match. Atlas `Listings` has the `photoUrl` and `photos.files` has the image. Open the `photoUrl` in a browser. |
| 5 | 5:30 | Match detail on the phone shows crop, distance and score, but not contact details. Tap **Confirm**. Cut to the Farmer device: **Match confirmed** notification. Contact details now show. Farmer marks the match **Completed**. Cut to the phone: **Match completed** notification, then give a thumbs-up rating. | Contact details stay hidden until a match is confirmed. Every status change notifies the other party. Ratings are one per person and only after completion. | Atlas `Matches` status changes Suggested, Confirmed, Completed; `Ratings` gains a document. |
| 6 | 7:15 | Buyer phone: **Home** (the ranked feed with distances), then **Calendar**: pick the harvest date and show the produce expected. Show **Listings** or the demand board. | These are the project's own features from the Part 1 design: the proximity match feed with its four-factor score, the harvest calendar, the demand board, and the trust rating. | Each of the five features appears on screen at least once. |
| 7 | 8:15 | Phone: switch on aeroplane mode (show the status bar). Post a new demand (Maize). The app says it is saved on the phone and shows it as pending. Switch aeroplane mode off. Show it uploading. | Records created offline are stored in Room with a client-generated id, and an outbox uploads them when the connection returns. If the upload is retried, the id stops a duplicate. | Atlas `Demands` now shows exactly one Maize document. Show the count before and after. |
| 8 | 9:45 | Laptop: Firebase Users, Atlas collections, Render logs, side by side. | A recap of where each piece of data lives: identities in Firebase, application data in Atlas, requests through the API. | All three show the activity from this video. |
| 9 | 10:30 | GitHub Releases page with the signed APK and bundle, the checksum file, then the Actions page with green runs. Back to the phone with the installed app and icon. | The release workflow builds and signs the APK from a tag. The app has its own icon, name, version and signing key, and is prepared for Play Store publication. | The signing certificate fingerprint matches the one registered in Firebase. |
| 10 | 11:15 | Title card with the repository link. | Thank the viewer and name the repository and the `Final-POE` tag. | Under 12 minutes. |

## Tips for recording

- Record the phone with its built-in screen recorder and the laptop with a screen recorder. Edit them together afterwards, so a failed take in one segment does not cost the rest.
- Record the voice-over live, or afterwards over the footage. Either is fine, as long as it explains what is happening and why.
- Cut waiting time (Render waking up, an upload) rather than leaving silence.
- Blur or crop anything private: the Firebase service account, connection strings, the keystore, and real phone numbers.
- Export in 1080p or lower to keep the file small, and upload it to YouTube as **Unlisted**.
- Put the link in the README under "Video walkthrough", replacing the Part 2 link, and keep the same link for the tag commit.

## If something fails mid-take

| Problem | Quick fix |
| --- | --- |
| No notification on the phone | Check the Buyer opened the app after signing in (token registers then), the notification permission is granted, and the Render log says push is enabled. |
| No match appears | Same crop spelling on both, the listing within the search radius, the demand deadline in the future, and the two accounts different. |
| Google sign-in fails with error 28444 | The installed build's signing certificate is not registered in Firebase. The release fingerprint is in the Final Release Checklist. |
| The first API call hangs | Render was asleep. Wait for it to wake, then cut the delay. |
| A queued record does not upload | Reconnect fully, open the app, and use the retry action on the pending record. |
