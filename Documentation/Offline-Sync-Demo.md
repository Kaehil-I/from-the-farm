# Offline sync: how to demonstrate and verify it

This is the evidence for the Part 3 offline requirement: a record created offline, exactly one copy after reconnection. Use a phone or emulator with the app installed and signed in as a **Farmer**, and keep MongoDB Atlas open next to it.

## Before you start

1. The live API must be running the build that contains the duplicate check. Open `https://from-the-farm.onrender.com/api/v1/health` and confirm the commit hash matches the latest commit on the branch you deployed. Without this the app still queues and uploads, but the server will not ignore a repeated id.
2. In Atlas, open the `listings` collection under Browse Collections and filter with `{ "clientGeneratedId": { "$exists": true } }`. Note how many documents match (for a fresh database, zero).
3. In Atlas, open the Indexes tab of `listings`. There should be a unique, partial index on the farmer and `clientGeneratedId`. If an old index named `clientGeneratedId_1` is still there, drop it.
4. Wake the API with the health request above so Render's cold start is not mistaken for being offline.

## The demonstration

| Step | Action | Expected result | Capture |
| --- | --- | --- | --- |
| 1 | Switch the phone to aeroplane mode (on the emulator, use the quick settings panel). | The status bar shows no connection. | Screenshot |
| 2 | Open **Listings**, tap **Add listing**, fill in crop, quantity, date and location, and press **Save**. | The form closes and the message "Saved on this phone. It will upload automatically when you're back online." appears. No error. | Screenshot |
| 3 | In Atlas, refresh the `listings` filter. | The count is **unchanged**. The record exists only on the phone. | Screenshot with the count visible |
| 4 | Turn aeroplane mode off. | Within a few seconds the app shows "Uploaded 1 record saved while offline." If the app was closed, the background job uploads it once a network is available. | Screenshot |
| 5 | In Atlas, refresh the `listings` filter. | The count is up by **exactly one**, and the new document has a `clientGeneratedId`. | Screenshot with the count and the document |
| 6 | Wait a minute, refresh again, and tap **Refresh** in the app. | Still exactly one new document, and one card in My listings. | Screenshot |

## Proving the duplicate protection

Steps 1 to 6 show the normal case. The case the design exists for is a connection that drops after the server has saved the record but before the phone hears back. The automated tests cover it (`OfflineQueueTest.aLostResponseDoesNotCreateTheRecordTwice` for the app and `ListingsControllerTests.CreateListing_ReturnsTheOriginal_WhenTheSameClientGeneratedIdArrivesTwice` for the API, which sends the same id twice and checks one record exists), so that is where to point if asked. To show it live, repeat the demonstration but turn aeroplane mode on the moment you press **Save**; if the request reached Render first, the record is already in Atlas and the later retry still leaves a single document.

## Things to know when presenting

- Editing and deleting a record need a connection. Only creating is queued.
- Start the app and sign in while online. A cold start without a connection cannot load the profile, so go offline after the app is open. Records already queued still upload from the background if the app is closed.
- The pending and failed records are available to the screens through `FarmViewModel.sync`. Whether a "waiting to upload" label is visible depends on the UI work for that state; the message in step 2 and the Atlas counts are the evidence that does not.
- A listing photo is still stored inline with the record until photos move to object storage, so a queued record with a photo is larger and uploads more slowly on a weak connection.
- If nothing uploads after reconnecting, open the app (it uploads on start and on reconnect), then check the API is awake with the health request.
