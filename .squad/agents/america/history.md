# Project Context

- **Owner:** Jeffrey T. Fritz
- **Project:** GitHub App that attaches to workshop repos, analyzes issues requesting tech upgrades, uses Copilot to review content, updates code samples and prose, and delivers PRs with changes.
- **Stack:** C#, .NET, GitHub Apps, GitHub Copilot API, Webhooks
- **Created:** 2026-02-13

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-02-13): Webhook handling will use Octokit.Webhooks.AspNetCore for signature validation and event routing; PRs will use multi-commit strategy by category for easier review; dual trigger mechanism supports both label and assignment workflows — decided by Kamala

📌 Team update (2026-02-13): Single-project architecture for Phase 1 (WorkshopManager.Api only); UpgradeIntent immutable record model with complete set of fields; IIssueParser interface established for async parsing with regex + keyword extraction strategy — decided by Kamala

📌 Team update (2026-02-13): Parallel development mocking strategy enabled — webhook endpoint, Copilot integration, and test infrastructure can all proceed with mocks against agreed-upon interfaces; integration happens after all streams land — decided by Kamala

📌 Team update (2026-02-14): CopilotContext and CopilotResponse fields aligned with design review spec (FromVersion/ToVersion, TokensUsed, RepositoryFullName/FilePath); Octokit.Webhooks uses ValueTask not Task for processor overrides; UpgradeIntent.Empty sentinel added — decided by America, Riri

📌 Fix (2026-02-14): All 10 integration tests (WebhookEndpointTests) were failing because IManifestParser was not registered in DI. WorkshopAnalyzer depends on IManifestParser (added in WI-10) but the Program.cs registration was missed. Added `builder.Services.AddSingleton<IManifestParser, ManifestParser>()` — single line fix. Key pattern: when adding new service dependencies to existing services, always update Program.cs DI registrations. — fixed by America

📌 Team update (2026-02-22): Milestone 1 complete — all 135 tests passing (124 unit + 11 integration). Kate fixed 5 unit test bugs (YAML quoting, case-insensitive matching, regex patterns, assertions). Riri shipped GitHubContentProvider with Octokit v14.0.0. Full webhook → Parser → Analyzer → Copilot pipeline now operational — decided by Scribe

📌 WI-17 (2026-02-22): Implemented IPullRequestService + PullRequestService for PR generation. Uses Git Data API (Trees + Commits + References) for remote-first multi-commit creation. Branch naming: workshop-upgrade/{issue}-{tech}-{version}. Commit order: ProjectFile → CodeSample → Documentation → Configuration. PR body includes summary stats table, changes by category, failures section, and REVIEW markers. Idempotent: resets branch + updates existing PR on re-run. Also created placeholder models (TransformationResult, TransformationSummary, PullRequestResult, UpgradeResult) matching design §2 for Riri coordination. DI registered as Scoped in Program.cs. — implemented by America
