# Part 3 Android Device Test Checklist

Use a physical Android phone for the final evidence. Record the Android version, device model, app version, tester and date before starting. Capture screenshots or video clips for every passed section.

## Installation and launch

- [ ] Install the current release build on the physical phone.
- [ ] Launch the app from the installed launcher icon.
- [ ] Confirm the app opens without a crash or missing configuration message.
- [ ] Confirm Google sign-in opens and returns to From The Farm.
- [ ] Confirm the profile, listings, demands and matches load from the hosted API.

## Biometric authentication

- [ ] Enrol a fingerprint or supported face unlock in Android Settings.
- [ ] Enable biometric unlock in From The Farm settings.
- [ ] Put the app in the background and reopen it.
- [ ] Confirm farm information remains hidden until authentication succeeds.
- [ ] Cancel the prompt and confirm the app stays locked.
- [ ] Submit an unrecognised fingerprint or face and confirm the app stays locked.
- [ ] Authenticate successfully and confirm the saved Firebase session resumes.
- [ ] Select **Lock app** and confirm the prompt is required again.
- [ ] Disable biometric unlock, background the app and confirm it no longer locks.
- [ ] Sign out and confirm biometric enrolment for that account is removed from the device.

## Navigation and interface

- [ ] Confirm Home, Listings/Demand, Calendar and Settings remain reachable from bottom navigation.
- [ ] Confirm the selected tab is visually clear.
- [ ] Confirm loading, empty, success and error messages are readable.
- [ ] Confirm controls are not hidden by the keyboard or system navigation.
- [ ] Enable the largest normal system font size and confirm essential controls remain usable.
- [ ] Enable TalkBack and confirm icon-only actions have meaningful descriptions.

## Farmer workflow

- [ ] Switch to Farmer mode without signing out.
- [ ] Create a listing using device location, a future harvest date and a photograph.
- [ ] Confirm invalid quantity, date and location values are rejected without a crash.
- [ ] Edit the listing and confirm the updated information appears.
- [ ] Open the calendar, select the harvest date and confirm the listing is shown.
- [ ] Remove the listing and confirm it no longer appears after refresh.

## Buyer workflow

- [ ] Switch to Buyer mode without signing out.
- [ ] Create a demand and confirm the location belongs to the buyer's demand.
- [ ] Confirm crop matching ignores capitalisation.
- [ ] Confirm matching products appear above other nearby products on Home.
- [ ] Use **Find nearby produce** and confirm the full catalogue is not erased.
- [ ] Tap an available listing and confirm only its crop type is copied into a new demand.
- [ ] Confirm the new demand's quantity, deadline and location remain empty.

## Match and rating workflow

- [ ] Open a suggested match and confirm private contact details are hidden.
- [ ] Confirm the match and verify that contact details become visible.
- [ ] Mark the exchange completed.
- [ ] Submit one positive or negative rating.
- [ ] Confirm a second rating cannot be submitted for the same match by the same user.

## Final evidence

- [ ] Run the automated Android tests and retain the successful output.
- [ ] Run Android lint and retain the report showing zero errors.
- [ ] Record the final demonstration on the same physical phone.
- [ ] Include authentication, settings, API data, the calendar, both roles and the match lifecycle.
- [ ] Repeat this checklist after offline sync, notifications and translations are merged.
