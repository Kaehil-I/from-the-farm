# Test matrix, regression checklist and translation checklist

This is the manual test record for the Part 3 localisation work and the regression checks that go with it. It only records what was actually run. A row marked **Not run** has not been tested, and each one says why.

## Environments

| Id | Environment | Used for |
| --- | --- | --- |
| E1 | Android Studio emulator `Medium_Phone` (API level 37, 4 GB RAM), build from branch `kyra-localisation-p3` | Localisation checks that do not need a signed-in user |
| E2 | Physical phone, signed release build | **Not available yet.** Needed for the signed-in, notification and biometric rows |
| CI | `gradlew test` on the developer machine and in Android CI | Unit tests |

**Status legend:** Pass, Fail, Not run (with the reason).

**Why most rows are Not run:** Google sign-in only works for builds signed with a key registered in the team's Firebase project. The debug key on the machine used for the emulator checks is not registered yet, so nothing behind sign-in (Settings, Home, Listings, Calendar, match details) could be reached. Those rows need either the key to be registered or a teammate's registered build.

## A. Automated unit tests

| Id | Check | Result | Evidence |
| --- | --- | --- | --- |
| A-01 | `gradlew :app:testDebugUnitTest` runs all Android unit tests | **Pass**: 65 tests, 0 failures, 0 skipped | Run at commit `58ade8f` (test result XML under `app/build/test-results`) |
| A-02 | Validation helper returns a string resource for each rule (crop, quantity, unit, date, past deadline, latitude, longitude) | **Pass** | `FormValidationTest` (5 tests) |
| A-03 | ViewModel reports messages as translatable keys instead of English text | **Pass** | `FarmViewModelTest`, `OfflineQueueTest`, `SettingsProfileTest` (assert the message key) |
| A-04 | The three strings files define the same keys | **Pass**: 191 keys in `values`, `values-zu` and `values-af`, plus 2 notification channel keys each | Key lists compared at commit `58ade8f` |

## B. Localisation tests

| Id | Test | Steps | Expected | Result |
| --- | --- | --- | --- | --- |
| L-01 | Sign-in screen translates | Set the app language to isiZulu, then Afrikaans, using Android's per-app language setting (`cmd locale set-app-locales`), and look at the sign-in screen | Heading, tagline, instruction and button are all in the chosen language | **Pass** on E1 for English, isiZulu and Afrikaans. This used the system setting, not the in-app chips |
| L-02 | Error message translates | With "Sign-in cancelled" showing, change the app language | The message changes language without being triggered again | **Pass** on E1 (English, Afrikaans, isiZulu) |
| L-03 | In-app language chips | Sign in, open Settings | Chips for English, isiZulu and Afrikaans are shown, with the saved language selected | Not run: sign-in blocked |
| L-04 | Save applies the language everywhere | In Settings pick isiZulu, press **Save preferences** | Whole app switches: bottom navigation, Home, Listings, Calendar, Settings, dialogs | Not run: sign-in blocked |
| L-05 | Language survives a restart | After L-04, force-stop and reopen | App opens in isiZulu | Not run: sign-in blocked |
| L-06 | Onboarding picks language | New user, choose Afrikaans on the profile screen, press **Continue** | Next screens are in Afrikaans | Not run: needs a new account |
| L-07 | Language follows the profile on a new install | Sign in on a fresh install with a profile saved as Afrikaans | App opens in Afrikaans | Not run: sign-in blocked |
| L-08 | Status labels | Open a match at each stage and the My listings or Demand cards | Suggested, Confirmed, Completed, Open and Active show translated; unknown values show as received | Not run: sign-in blocked |
| L-09 | Validation messages | Save a form with an empty crop, a bad quantity, a bad date, a past deadline and out-of-range coordinates, once per language | Each message appears in the active language | Not run: sign-in blocked (the logic is unit tested, A-02) |
| L-10 | Action messages | Save preferences, post a demand, queue a record offline, upload it, complete an exchange, submit a rating | Each confirmation appears in the active language | Not run: sign-in blocked (message keys are unit tested, A-03) |
| L-11 | Biometric prompts and errors | Enable biometric unlock, cancel the prompt, fail it, and try on a device with none enrolled | Prompt title, subtitle, Cancel button and errors are translated | Not run: needs a device with biometrics (E2) |
| L-12 | Notification channel name | Android Settings, Apps, From The Farm, Notifications | Channel name and description are in the device language (isiZulu or Afrikaans) | Not run: needs E2 |
| L-13 | Push text language | Trigger a new match for a user whose profile language is Afrikaans | The push title and body are in Afrikaans (written by the API) | Not run: needs E2 and a working push setup |
| L-14 | Long text fits | Largest font size in Android settings, each language | No clipped or overlapping text on Settings, forms and match details | Not run: sign-in blocked |
| L-15 | Dates and numbers | Open the Calendar and a form date picker in each language | The date picker follows the system locale; app text stays consistent | Not run: sign-in blocked |
| L-16 | Language chips show native names | Compare the chips in each language | Always "English", "isiZulu", "Afrikaans" | Not run: sign-in blocked |

## C. Regression checklist

Run after any change to screens, the ViewModel or the API contract. Nothing here has been re-run on a device for this branch.

| Id | Area | Check | Result |
| --- | --- | --- | --- |
| R-01 | Launch | App starts without crashing on the sign-in screen after the `AppCompatActivity` and theme change | **Pass** on E1 |
| R-02 | Sign-in | Google sign-in succeeds and the profile loads | Not run: debug key not registered in Firebase |
| R-03 | Onboarding | New user picks role, language, radius and phone | Not run |
| R-04 | Role switch | Switch Farmer to Buyer and back without signing out | Not run |
| R-05 | Listings | Create, edit (including photo) and delete a listing | Not run |
| R-06 | Demands | Create, edit and delete a demand; browse listings and open a demand from a card | Not run |
| R-07 | Nearby search | Search with a location, radius and crop (case-insensitive) | Not run |
| R-08 | Calendar | Pick a date and see produce for that harvest day | Not run |
| R-09 | Match lifecycle | Suggested, Confirm, Complete, rate; contact details only after confirmation | Not run (API lifecycle covered by backend unit tests) |
| R-10 | Offline create | Follow `Documentation/Offline-Sync-Demo.md`: queue offline, one record after reconnect | Not run on this branch (Gregory's unit tests pass, A-01) |
| R-11 | Biometrics | Enable, lock, unlock, disable | Not run: needs E2 |
| R-12 | Notifications | Permission prompt after onboarding; tapping a push opens that match | Not run: needs E2 |
| R-13 | Sign out | Cached data cleared; unsent records stay for the same account | Not run |
| R-14 | Location | Use current location, deny permission, manual entry | Not run |

## D. Translation checklist

All text lives in `app/src/main/res/values/strings.xml` (English), `values-zu/strings.xml` (isiZulu) and `values-af/strings.xml` (Afrikaans), plus `notification_strings.xml` for the notification channel. The isiZulu and Afrikaans text is a **first draft written without a native-speaker review**. The review column is for the reviewer to fill in.

| Group | Strings | English | isiZulu | Afrikaans | Reviewed by / date |
| --- | --- | --- | --- | --- | --- |
| App name (kept as "From The Farm") | 1 | Done | Same as English | Same as English | |
| Navigation and common actions | 11 | Done | Draft | Draft | |
| Status labels | 8 | Done | Draft | Draft | |
| Locked screen, sign-in and profile load | 12 | Done | Draft | Draft | |
| Biometric setup, prompts and errors | 15 | Done | Draft | Draft | |
| Record unavailable and remove dialog | 3 | Done | Draft | Draft | |
| Match details | 15 | Done | Draft | Draft | |
| Home | 13 | Done | Draft | Draft | |
| Listings, demands and nearby search | 15 | Done | Draft | Draft | |
| Calendar | 4 | Done | Draft | Draft | |
| Listing and demand forms | 16 | Done | Draft | Draft | |
| Photos | 7 | Done | Draft | Draft | |
| Location | 13 | Done | Draft | Draft | |
| Profile and settings | 15 | Done | Draft | Draft | |
| Action messages and errors | 36 | Done | Draft | Draft | |
| Form validation | 7 | Done | Draft | Draft | |
| Notification channel name and description | 2 | Done | Draft | Draft | |
| Push notification text (written by the API, `NotificationMessages.cs`) | 3 messages | Done | Written by the API team, not reviewed | Written by the API team, not reviewed | |

The group counts add up to 191 strings plus the 2 notification channel strings. Push text is not in the Android resources, so it is listed separately.

### Terms to keep consistent

| English | isiZulu | Afrikaans |
| --- | --- | --- |
| match (noun) | ukufaniswa | passing |
| listing | uhlu | lys |
| demand (request) | isidingo | aanvraag |
| farmer | umlimi | boer |
| buyer | umthengi | koper |
| harvest | isivuno / ukuvuna | oes |
| settings | izilungiselelo | instellings |
| refresh | vuselela | verfris |
| sign in | ngena | meld aan |
| sign out | phuma | teken uit |

### Checks a reviewer should make

- The wording is natural and correct for a smallholder farmer, not a literal translation.
- Placeholders (`%1$s`, `%1$d`) are in a position that reads correctly in the sentence.
- Number-and-unit combinations such as "ama-%1$d km" (isiZulu) read correctly.
- Technical terms (Google, biometrics, JPEG, PNG, YYYY-MM-DD) are acceptable as written.
- The brand name "From The Farm" stays in English everywhere except the screen heading ("Van die plaas", "Evela epulazini").

## E. Gaps in coverage

- Everything behind sign-in is unverified on a device for this branch. Rows L-03 to L-16 and R-02 to R-14 need a registered signing key (or a teammate's build) and ideally a physical phone.
- The in-app language chips have not been pressed. Only the underlying system language mechanism has been exercised.
- Right-to-left and other scripts are out of scope (English, isiZulu and Afrikaans are all left-to-right).
- The Part 1 preview screens still contain English text. They are not wired into the app, so they are not translated.
