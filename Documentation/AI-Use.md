# AI-use disclosure

This document records how generative AI assisted the From The Farm project and where human review remained necessary. It is included for transparency and must be read together with the commit history, pull request reviews, test matrix and release checklist.

## Tool used

The team used OpenAI Codex as a coding assistant. OpenAI describes Codex as a coding agent that can help users write, review and ship code, and provides it through desktop, terminal, IDE and web clients (OpenAI, 2026a; OpenAI, 2026b).

## How it assisted

Codex was used to:

- inspect the existing Kotlin, Compose, ASP.NET Core and documentation structure;
- suggest and implement scoped code changes under team direction;
- diagnose Gradle, Android Studio, CI, Git and branch-integration problems;
- review diffs and pull requests for mergeability, dependencies, security and test coverage;
- run or interpret automated tests, builds, lint and repository checks;
- draft commit messages, pull request descriptions, setup instructions and technical documentation;
- compare implementation claims with the repository and identify evidence that still needed a person or physical device.

## Human responsibility and verification

AI output was not treated as proof that a feature worked. Team members remained responsible for:

- deciding the requirements, architecture, roles and final scope;
- reviewing proposed changes before merge;
- protecting Firebase, MongoDB, Render and signing credentials;
- approving pull requests and resolving team ownership;
- testing Google sign-in, biometrics, notifications, location and offline behaviour on actual devices;
- checking isiZulu and Afrikaans wording with suitable language reviewers;
- confirming that screenshots, video, database records and test evidence were genuine;
- deciding whether the final build met the assessment brief before submission.

Automated checks are reported only when their output was observed. Device-only or cloud-only behaviour is marked **Not run**, **Partial** or **Unverified** when suitable evidence was unavailable. No passwords, private keys, fabricated test results, fabricated screenshots or fabricated peer approvals should be added to the submission.

## Academic-integrity boundary

The repository remains the team's work product. AI assistance does not replace the team's understanding of the code. Every member should be able to explain their contribution, the data flow, the security decisions, the test results and the known limitations during the demonstration or assessment. This disclosure should be adjusted if the institution requires a specific AI declaration format.

## References

OpenAI. 2026a. *Using Codex with your ChatGPT plan*. Available at: https://help.openai.com/en/articles/11369540-using-codex-with-your-chatgpt-plan (Accessed: 6 November 2026).

OpenAI. 2026b. *Codex*. Available at: https://developers.openai.com/learn/codex (Accessed: 6 November 2026).

OpenAI. 2026c. *Review pull requests with Codex*. Available at: https://help.openai.com/en/articles/20001552-review-pull-requests-with-codex (Accessed: 6 November 2026).
