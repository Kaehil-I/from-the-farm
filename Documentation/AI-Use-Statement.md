# AI use statement

**Group statement.** AI was used as an assistant on this project. A team member reviewed every change before it was pushed. Commits carry the team member's name only, so AI assistance is declared here and not in commit trailers. In text it is cited as (Anthropic, 2026) and listed in the README's References.

## Tool

Claude Sonnet 5.5, used through Claude Code (Anthropic, 2026), in Kyra Naidoo's working session.

## What it was used for: Kyra Naidoo

- **Localisation code.** Moving visible text into Android string resources, switching the app language from the profile's preferred language, and making ViewModel messages and form validation translatable.
- **Translations.** First drafts of the isiZulu and Afrikaans text. A native speaker has not reviewed them, so treat them as drafts.
- **Merging and tests.** Resolving the merge conflict with the offline sync screen and translating its text, updating five existing test assertions, and writing a temporary emulator test that checked the screens in all three languages with sample data. That test is not in the repository.
- **Documentation.** The test matrix, translation checklist, release notes, the Localisation and Store publication readiness sections of the README, and the pull request descriptions.
- **Diagnosis and review.** Finding why Google sign-in failed on one machine (its signing key is not registered in Firebase), and reading a teammate's pull request (#15) before it was approved from Kyra's account.

## Other members

This statement covers Kyra Naidoo's use only. Each of the others should add what they used, with the same detail.

- Zario Di Paolo: *to be added*
- Kaehil Indurjeeth: *to be added*
- Gregory Luyckfasseel: *to be added*

## How the output was checked

- Each commit and pull request text was shown to Kyra and approved before it was pushed.
- All 65 Android unit tests pass, and the temporary emulator test passed in English, isiZulu and Afrikaans.
- Not checked: Google sign-in with a real account, the live API, and a physical phone. AI-written code and text can contain mistakes, and the translations still need a native-speaker review.

## Citation

Anthropic. 2026. *Claude Code* [AI coding assistant, Claude Sonnet 5.5]. Available at: https://claude.com/claude-code (Accessed: 6 November 2026).
