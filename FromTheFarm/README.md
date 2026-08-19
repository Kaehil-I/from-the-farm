# From The Farm — UI prototype (Jetpack Compose)

This is a starting Android Studio project containing the 10 screens from the design mockups, built as Jetpack Compose composables with sample/mock data. No API, database, or authentication is wired up yet — this is UI-only, matching the Part 1 design phase.

## How to open

1. Open Android Studio (Hedgehog or newer recommended).
2. **File > Open**, select the `FromTheFarm` folder.
3. Android Studio will detect there's no Gradle wrapper jar and offer to generate one automatically — accept this, or run **File > Sync Project with Gradle Files** once it's added.
4. Let Gradle sync (downloads the dependencies listed in `app/build.gradle.kts`).
5. Run on an emulator or physical device, or open any screen file and use the `@Preview` composable at the bottom of the file to see it directly in the Compose Preview pane without running the app.

## Project structure

```
app/src/main/java/com/fromthefarm/app/
├── MainActivity.kt              — entry point, hosts the nav graph
├── data/SampleData.kt           — mock listings, demand requests, calendar events
├── ui/theme/                    — Color.kt, Type.kt, Theme.kt (matches the mockup palette)
├── ui/navigation/FarmNavHost.kt — routes, bottom nav bar, screen wiring
└── ui/screens/                  — one file per screen (10 total)
```

## Known gaps — intentional for this stage

- All data is hardcoded in `SampleData.kt`. Once Kaehil's REST API schema is finalised, this gets replaced with real network calls (Retrofit) and a ViewModel per screen.
- `CreateListingScreen.kt` and `CreateDemandScreen.kt` have a comment flagging that their fields must mirror the API's schema exactly — check these first once the schema lands.
- The bottom "Listings" tab always shows `MyListingsScreen`. Role-based switching (farmer sees listings, buyer sees `BuyerDemandBoardScreen`) still needs to be wired in `FarmNavHost.kt` based on the signed-in user's role from onboarding.
- No SSO, biometric auth, offline storage, or push notifications are implemented yet — those are Part 2 / final POE features.
- No unit tests or GitHub Actions workflow yet.

## Next steps

1. Confirm this compiles cleanly in Android Studio and fix any Gradle version mismatches for your installed AGP/Kotlin versions if needed.
2. Once Kaehil shares the finalised endpoint schemas, update the form fields in `CreateListingScreen.kt` / `CreateDemandScreen.kt` and the fields in `SampleData.kt` to match exactly.
3. Push this to the group's GitHub repository as the starting point for Part 2.
