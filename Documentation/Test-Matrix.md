# Test matrix, regression checklist and translation checklist

This is the manual test record for the Part 3 localisation work and the regression checks that go with it. It only records what was actually run. A row marked **Not run** has not been tested, and each one says why.

## Environments

| Id | Environment | Used for |
| --- | --- | --- |
| E1 | Android Studio emulator `Medium_Phone` (API level 37, 4 GB RAM), build from branch `kyra-localisation-p3` | Localisation checks that do not need a signed-in user |
| E1b | Same emulator and build, with the real screens and the real ViewModel running inside a throwaway test screen that uses fake data (no Google sign-in) | The signed-in screens: Farmer and Buyer Home, Listings or Demand, forms, Calendar, Settings, match details, the locked screen and language switching, in English, isiZulu and Afrikaans. Run on 5 November 2026 with a temporary instrumented test (not in the repository) that checks each expected translated string is on screen and saves screenshots. The 5 tests pass |
| E2 | Physical phone, signed release build | **Not available yet.** Needed for the notification, biometric dialog and real-account rows |
| CI | `gradlew test` on the developer machine and in Android CI | Unit tests |

**Status legend:** Pass, Partial (some of the row was checked, the rest says what was not), Not run (with the reason), Finding (a problem or oddity worth acting on).

**What E1b does and does not prove:** it proves the app's screens, translations and language switching work with the real code. It does not prove sign-in, the real API, or behaviour on a real phone. Google sign-in only works for builds signed with a key registered in the team's Firebase project, and the debug key on this machine is not registered yet, so the real-account rows are still Not run.

## A. Automated unit tests

| Id | Check | Result | Evidence |
| --- | --- | --- | --- |
| A-01 | `gradlew :app:testDebugUnitTest` runs all Android unit tests | **Pass**: 65 tests, 0 failures, 0 skipped | Run at commit `58ade8f` (test result XML under `app/build/test-results`) |
| A-02 | Validation helper returns a string resource for each rule (crop, quantity, unit, date, past deadline, latitude, longitude) | **Pass** | `FormValidationTest` (5 tests) |
| A-03 | ViewModel reports messages as translatable keys instead of English text | **Pass** | `FarmViewModelTest`, `OfflineQueueTest`, `SettingsProfileTest` (assert the message key) |
| A-04 | The three strings files define the same keys | **Pass**: 209 keys in `values`, `values-zu` and `values-af`, plus 2 notification channel keys each | Key lists compared at commit `58ade8f` |

## B. Localisation tests

| Id | Test | Steps | Expected | Result |
| --- | --- | --- | --- | --- |
| L-01 | Sign-in screen translates | Set the app language to isiZulu, then Afrikaans, using Android's per-app language setting (`cmd locale set-app-locales`), and look at the sign-in screen | Heading, tagline, instruction and button are all in the chosen language | **Pass** on E1 for English, isiZulu and Afrikaans. This used the system setting, not the in-app chips |
| L-02 | Error message translates | With "Sign-in cancelled" showing, change the app language | The message changes language without being triggered again | **Pass** on E1 (English, Afrikaans, isiZulu) |
| L-03 | In-app language chips | Open Settings | Chips for English, isiZulu and Afrikaans are shown, with the saved language selected | **Pass** on E1b in all three languages (screenshot checked for isiZulu) |
| L-04 | Save applies the language everywhere | In Settings pick isiZulu, press **Save preferences**, then Afrikaans, then English | Whole app switches each time: bottom navigation, Settings, Home | **Pass** on E1b: English to isiZulu to Afrikaans to English, the whole screen changed each time |
| L-05 | Language survives a restart | After L-04, force-stop and reopen | App opens in the saved language | **Partial** on E1b: reopening with a saved profile language opens in that language (see L-07). A real process kill with a real account was not run |
| L-06 | Onboarding picks language | New user, choose Afrikaans on the profile screen, press **Continue** | Next screens are in Afrikaans | Not run: the test screen starts from an already-onboarded profile |
| L-07 | Language follows the profile on a new install | Launch with no app language set and a profile saved as Afrikaans | App opens in Afrikaans | **Pass** on E1b |
| L-08 | Status labels | Open a match at each stage and the My listings or Demand cards | Suggested, Confirmed, Completed, Open and Active show translated; unknown values show as received | **Partial** on E1b: Suggested, Confirmed, Active and Open verified in all three languages. Completed, Matched, Expired and Deleted were not shown on the screens tested |
| L-09 | Validation messages | Save a form with an empty crop, a bad quantity, a bad date, a past deadline and out-of-range coordinates, once per language | Each message appears in the active language | **Partial** on E1b: the crop-name message on the listing and the demand forms verified in all three languages. The other six messages are only covered by the unit tests (A-02) |
| L-10 | Action messages | Save preferences, post a demand, queue a record offline, upload it, complete an exchange, submit a rating | Each confirmation appears in the active language | **Partial** on E1b: "Rating submitted" and "You have already rated" verified in three languages. **Finding F-01:** "Preferences saved" and "Exchange marked completed" were not on screen within 3 seconds. Not run: demand posted or updated, saved offline, uploaded, products found |
| L-11 | Biometric prompts and errors | Enable biometric unlock, cancel the prompt, fail it, and try on a device with none enrolled | Prompt title, subtitle, Cancel button and errors are translated | **Partial** on E1b: the locked screen text and the translated "no fingerprint enrolled" error verified in three languages. The system fingerprint dialog (title, subtitle, Cancel) needs enrolled biometrics (E2) |
| L-12 | Notification channel name | Start the app in each language and read the channel from `dumpsys notification` | Channel name is in the app language | **Pass** on E1: "Match updates", "Izibuyekezo zokufaniswa", "Passingopdaterings". The channel description was not read. The Android Settings screen itself was not looked at |
| L-13 | Push text language | Trigger a new match for a user whose profile language is Afrikaans | The push title and body are in Afrikaans (written by the API) | Not run: needs E2 and a working push setup |
| L-14 | Long text fits | Largest font size in Android settings, each language | No clipped or overlapping text on Settings, forms and match details | Not run at the largest font size. At the default size the screenshots show no clipped or overlapping text on Home, Settings and the locked screen |
| L-15 | Dates and numbers | Open the Calendar and a form date picker in each language | The date picker follows the system locale; app text stays consistent | **Partial** on E1b: the Calendar title, hint and "select a date" text verified in all three languages. The date picker's own text was not examined. Distances use the language's decimal comma (for example "1,2 km" in Afrikaans) |
| L-16 | Language chips show native names | Compare the chips in each language | Always "English", "isiZulu", "Afrikaans" | **Pass** on E1b |
| L-17 | Offline sync UI | Go offline, create a listing, then read the sync banner and the saved-record card, and try Retry and Discard | Banner, card, buttons and dialog are in the active language | Not run: this screen arrived after the emulator test was run. Its text is in all three strings files, and the build and unit tests pass |

## C. Regression checklist

Run after any change to screens, the ViewModel or the API contract. The E1b results use fake data, so they check the screens and flows, not the real API.

| Id | Area | Check | Result |
| --- | --- | --- | --- |
| R-01 | Launch | App starts without crashing on the sign-in screen after the `AppCompatActivity` and theme change | **Pass** on E1 |
| R-02 | Sign-in | Google sign-in succeeds and the profile loads | Not run: debug key not registered in Firebase |
| R-03 | Onboarding | New user picks role, language, radius and phone | Not run |
| R-04 | Role switch | Switch Farmer to Buyer and back without signing out | Not run (the Farmer and Buyer screens were each opened separately on E1b) |
| R-05 | Listings | Create, edit (including photo) and delete a listing | **Partial** on E1b: the Listings screen shows the listing, and the add-listing form opens and validates. Saving, editing and deleting against the API were not run |
| R-06 | Demands | Create, edit and delete a demand; browse listings and open a demand from a card | **Partial** on E1b: the Demand screen, the recommendations and the post-demand form open and validate. Saving, editing and deleting were not run |
| R-07 | Nearby search | Search with a location, radius and crop (case-insensitive) | Not run (the search form is shown on E1b) |
| R-08 | Calendar | Pick a date and see produce for that harvest day | **Partial** on E1b: the Calendar screen renders with the date picker. Picking a date was not run |
| R-09 | Match lifecycle | Suggested, Confirm, Complete, rate; contact details only after confirmation | **Partial** on E1b: against a fake backend the screen goes Suggested, Confirmed, Complete dialog, Completed, rated, with the contact-hidden note shown while Suggested. The real API lifecycle is covered by the backend unit tests |
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
| Offline sync (banner, saved-record cards, discard dialog) | 18 | Done | Draft | Draft | |
| Notification channel name and description | 2 | Done | Draft | Draft | |
| Push notification text (written by the API, `NotificationMessages.cs`) | 3 messages | Done | Written by the API team, not reviewed | Written by the API team, not reviewed | |

The group counts add up to 209 strings plus the 2 notification channel strings. Push text is not in the Android resources, so it is listed separately.

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

## E. Findings

- **F-01: confirmation messages disappear almost at once.** After **Save preferences** and after **Complete exchange**, the green confirmation ("Preferences saved.", "Exchange marked completed.") was not on screen within 3 seconds in any language. The likely cause, from reading the code and not separately confirmed: both actions set `loaded = false`, which makes the screen start an automatic refresh, and every action clears the message when it starts. This is not a translation problem and it was there before the localisation work. The unit tests only check the message straight after the action. A possible fix is to keep the message until the person's next action. The ViewModel is shared, so the team should decide.

## F. Gaps in coverage

- The real-account rows still need a registered signing key (or a teammate's build): R-02 to R-04, R-07, R-10, R-13, R-14 and L-05 with a real process kill.
- The system biometric dialog, push delivery and the Android notification settings screen need a physical phone (E2).
- The tests ran on an API 37 emulator only. Android 8 to 12 (API 26 to 32), where AppCompat stores the language itself, were not run.
- The E1b test code is not in the repository. It is a throwaway harness kept outside it, so these results can be reproduced only by rebuilding that harness. The screenshots from the run are also not in the repository.
- Right-to-left and other scripts are out of scope (English, isiZulu and Afrikaans are all left-to-right).
- The Part 1 preview screens still contain English text. They are not wired into the app, so they are not translated.
