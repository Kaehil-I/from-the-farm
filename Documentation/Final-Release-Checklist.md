# Final release checklist

Work through this top to bottom on submission day. Do not create the tag or mirror to the university repository until every earlier section is ticked and the team has approved the final commit.

## Decisions to settle first

- [ ] The exact name of the required tag. Git tags cannot contain spaces, so "Final POE" must be written like `Final-POE` or `v1.0.0`. The Release workflow starts on tags beginning with `v` or containing `POE` or `poe`.
- [ ] Where Render deploys from after the merge to `main` (it currently follows `Part-3-Integration`).
- [ ] Whether confirming a match stays single-sided (either party releases both contact details) or becomes mutual. It is single-sided today; changing it also needs an Android change.

## 1. Freeze the code

- [ ] Every Part 3 pull request is merged into `Part-3-Integration` and has been approved by a teammate. No open PRs remain that should be in the release.
- [ ] Latest `Part-3-Integration` has green Backend CI and Android CI.
- [ ] `https://from-the-farm.onrender.com/api/v1/health` reports the same commit as the tip of the branch.
- [ ] The Render log shows `Push notifications enabled.`.
- [ ] `google-services.json` contains the release certificate fingerprint (`7baca34f20742f8e4214f86de6f26c498fd1059b`) and the same hash is registered in Firebase.

## 2. Verify on real devices

- [ ] Push notification demonstration recorded: [Notification-Demo.md](Notification-Demo.md).
- [ ] Offline sync demonstration recorded: [Offline-Sync-Demo.md](Offline-Sync-Demo.md).
- [ ] The **signed release APK** (from the Release workflow artifact) installed on a physical phone: Google sign-in works, biometric lock works, a listing can be created.
- [ ] Language switching changes the whole app in English, isiZulu and Afrikaans.
- [ ] Manual test matrix and regression checklist completed with results.

## 3. Documents

- [ ] README is current: setup, architecture, Part 3 features, video link and the test counts.
- [ ] Release notes cover everything since Part 2.
- [ ] AI-use write-up with citations.
- [ ] Demo video recorded and its link is in the README.
- [ ] Screenshots, signed-build evidence and Play Store assets collected.

## 4. Hygiene before the freeze

- [ ] Confirm no secret (database connection string, service account key, keystore password) sits in the repository, the README or the documents. If one was ever shared in chat, rotate it and update the matching Render setting, then confirm `/api/v1/health` still reports healthy.
- [ ] Delete downloaded copies of the Firebase service account key from Downloads.
- [ ] The release keystore is backed up somewhere safe and outside the repository. Losing it means updates cannot be signed with the same key.
- [ ] Nobody is still pushing to branches that matter. Tell the team the freeze time.

## 5. Merge to `main`

- [ ] Open a PR from `Part-3-Integration` to `main` and get it approved. `main` is protected: one approval is required.
- [ ] Merge it. The merge commit on `main` is the final commit.
- [ ] Record its hash here: `____________`.

## 6. Tag and build the release

- [ ] The team has approved that exact commit as final.
- [ ] Create the tag on the merge commit and push it:

```bash
git switch main
git pull
git tag -a TAGNAME -m "Final POE submission"
git push origin TAGNAME
```

- [ ] The **Release** workflow runs green on that tag (backend tests, Android tests, signed build).
- [ ] The GitHub Release exists with the APK, the app bundle and `SHA256SUMS.txt`.
- [ ] The certificate fingerprint printed in the "Verify the signature" step matches the one registered in Firebase.

## 7. Mirror to the university repository

The university repository is `EMKNDN/emkndn-prog7314-2026-prog7314-poe-kaehil-i`, set up as the `submission` remote. Its `main` carries one extra commit that adds a "Development repository" section to the README.

- [ ] Fetch both remotes and confirm the university `main` is an ancestor of the new `main`, so the push is a fast-forward and nothing is overwritten:

```bash
git fetch origin submission
git merge-base --is-ancestor submission/main origin/main && echo "fast-forward is safe"
```

- [ ] If that prints nothing, stop and compare the two histories; do not force-push.
- [ ] Read the README "Development repository" section on both repositories. It says the university repository is the submitted deliverable and that development happened on the other one. Make sure the wording is right for the repository it sits in.
- [ ] Push the code and the tag:

```bash
git push submission origin/main:refs/heads/main
git push submission TAGNAME
```

- [ ] Be aware that the tag push also triggers the Release workflow on the university repository, which fails at "Check signing secrets" unless the four `ANDROID_KEY*` secrets exist there. Either add them to that repository first (so it produces its own Release) or accept one red run and note it. Decide before pushing the tag.
- [ ] Confirm the result:

```bash
git ls-remote submission refs/heads/main refs/tags/TAGNAME
git ls-remote origin refs/heads/main refs/tags/TAGNAME
```

- [ ] Open the university repository in a browser: the README renders, the code is present, the tag shows under Releases or Tags and the commit history is intact.

## 8. After submitting

- [ ] Write down the final commit hash and the tag, and the date and time submitted.
- [ ] Do not push to either repository again until marks are released.
- [ ] Submit the links the course asks for.
