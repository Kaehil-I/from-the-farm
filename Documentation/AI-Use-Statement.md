# AI use statement: From The Farm team

**Summary.** Our team (Zario Di Paolo, Kaehil Indurjeeth, Gregory Luyckfasseel and Kyra Naidoo) used AI tools as assistants while building From The Farm. They did not replace our own work. We decided what to build and checked what the tools produced. Every change reached the shared `Part-3-Integration` branch through a pull request that needs a review, with CI on each. Each of us is responsible for what we submitted.

## Tool

Claude Sonnet 5.5, used through Claude Code (Anthropic, 2026).

## What AI was used for

- **Code.** Writing and refactoring Android code: moving screen text into string resources, switching the app language from the profile language, and making error messages and form validation translatable.
- **Interface design.** Help with the look of the screens and the app's colour scheme.
- **Translations.** First drafts of the isiZulu and Afrikaans text. A native speaker has not reviewed them.
- **Merging.** Resolving merge conflicts between members' branches, and translating the text a merge brought in.
- **Testing.** Writing and updating tests, including a temporary emulator test whose five tests opened the real screens with sample data in English, isiZulu and Afrikaans and checked each expected translated string. It showed that two confirmation messages disappear almost at once, which the test matrix records. That test is not in the repository.
- **Documentation.** Drafting the test matrix, translation checklist, release notes, README sections, pull request descriptions and this statement.
- **Diagnosis and review.** Finding why Google sign-in failed on one machine (its signing key was not registered in Firebase), and reading a teammate's pull request before it was approved.

<!-- Team: add here any other AI tool or use that is not covered above, before submitting. -->

## How we checked AI-assisted work

- The same route as everyone's work: a pull request, a review by another member, and CI.
- The member who used the tool approved each AI-assisted commit and pull request text before it was pushed.
- All 65 Android unit tests pass, and the emulator test passed in all three languages.
- The output needed correcting. A language picker the tool built duplicated the profile setting, so it was removed, and a bug that would have shown an exception's text as a message was caught before merging.

## Limits

- Not checked: Google sign-in with a real account, the live API, and a physical phone.
- AI output can be wrong, so we treat it as a draft that a person checks.
- The translations still need a native-speaker review.

## How it is cited

No commit message mentions an AI tool, so use is declared here and not in commit trailers. In the text the tool is cited as (Anthropic, 2026), with a full entry in the README's References.

Anthropic. 2026. *Claude Code* [AI coding assistant, Claude Sonnet 5.5]. Available at: https://claude.com/claude-code (Accessed: 6 November 2026).
