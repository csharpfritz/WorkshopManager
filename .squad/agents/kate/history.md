# Project Context

- **Owner:** Jeffrey T. Fritz
- **Project:** GitHub App that attaches to workshop repos, analyzes issues requesting tech upgrades, uses Copilot to review content, updates code samples and prose, and delivers PRs with changes.
- **Stack:** C#, .NET, GitHub Apps, GitHub Copilot API, Webhooks
- **Created:** 2026-02-13

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-02-13): v1 scope boundaries established — handles C#, project files, Markdown, JSON/YAML config, shell scripts; excludes binary/multimedia, Jupyter notebooks, complex migrations; multi-commit PR strategy by category enables partial cherry-picking — decided by Kamala

📌 Team update (2026-02-13): GitHub App configuration requirements established — permissions for issues, contents, pull_requests, metadata; webhook events for issues, issue_comment, label; OAuth installation flow required; webhook secret in environment variable only — decided by Kamala

📌 Team update (2026-02-13): Configuration via Options Pattern — GitHubAppOptions and CopilotSettings with [Required] validation and ValidateOnStart(); secrets in environment variables only; configuration additive for Phase 5 features — decided by Kamala

📌 Team update (2026-02-13): Webhook error handling strategy — log and comment on issue for failures; retry transient failures only (network, rate limit); parsing failures = no retry; user feedback balanced with system reliability — decided by Kamala

📌 Team update (2026-02-14): Release Notes and Dependabot Integration architecture — Trigger Classifier routes to issue parser, release notes parser, or Dependabot detector; both new triggers feed into Upgrade Processor with inferred intent; zero breaking changes to core workflow; GitHub App webhook expanded to include pull_request event — decided by Kamala

📌 WI-06/WI-07 (2026-02-14): Wrote 20 unit tests for IssueParser and 11 integration tests for webhook endpoints. All pass. Discovered app bug: IssueParser regex `(\S+)` captures only the first non-whitespace token from structured body fields like `**To:** .NET 9`, returning ".NET" instead of the full version string ".NET 9". Same issue affects `**From:**` fields. Title from-to pattern also fails for multi-word technology names like ".NET 8". Tests document this behavior with BUG comments. Integration tests confirm webhook HMAC validation, health check, and event processing work correctly. Used JSON deserialization approach to construct Octokit.Webhooks IssuesEvent objects in unit tests. Added InternalsVisibleTo for integration test project access to Program.

📌 WI-13 (2026-02-14): Wrote 89 passing tests for content discovery system — WorkshopAnalyzerTests (17 tests, 15 pass), FileClassifierTests (43 tests, all pass), TechnologyDetectorTests (29 tests, 27 pass), ManifestParserTests (15 tests, 12 pass). Covered all detection strategies (Convention, Manifest, Hybrid), technology priority ordering (.NET → Node → Python → Go → Java), module grouping (manifest-based and convention-based), file classification by extension/directory patterns, version extraction from project files, manifest parsing with partial/invalid YAML handling, excluded paths (.git/, node_modules/, bin/, obj/), and diagnostics for edge cases. Created 7 test fixtures (sample .csproj, package.json, pyproject.toml, go.mod, manifest-full.yml, manifest-partial.yml, manifest-invalid.yml) in Fixtures/ directory for reusable test data. Used InMemoryContentProvider for isolated unit tests without filesystem dependencies. All tests use xUnit + FluentAssertions + NSubstitute pattern matching existing test infrastructure.

📌 Unit test fix pass (2026-02-22): Fixed all 5 failing tests (124/124 now pass). Root causes: (1) manifest-full.yml had unquoted `*.bak` — YAML `*` is an alias indicator, must quote glob patterns in YAML fixtures. (2) ManifestParser needed `WithCaseInsensitivePropertyMatching()` for PascalCase YAML key support. (3) IssueParser TitleFromToPattern regex group 2 `(.+?)(?:\s|$)` stopped at first space in multi-word versions like ".NET 9" — fixed with `(\S+(?:\s+\d\S*)?)` which captures tech name + version number. (4) WorkshopAnalyzerTests expected 6 items but FileClassifier excludes `.workshop.yml` (line 188 in FileClassifier.cs), so correct count is 5. Key patterns: always quote YAML glob patterns; WorkshopAnalyzer.cs lines 50-55 still have TODO stub for manifest parsing (not yet calling _manifestParser.Parse).

📌 Team update (2026-02-22): Milestone 1 complete — all 135 tests passing (124 unit + 11 integration). America fixed DI registration bug (IManifestParser missing from Program.cs). Riri shipped GitHubContentProvider with Octokit v14.0.0 and scoped lifetime cascade. Full webhook → Parser → Analyzer → Copilot pipeline now operational — decided by Scribe

📌 Phase 3 test plan (2026-02-22): Created `docs/design/phase3-test-plan.md` covering ~80-115 tests across code transformation (CT-01 through CT-E07), docs transformation (DT-01 through DT-E05), PR generation (PR-01 through PR-E07), Copilot API errors (ERR-01 through ERR-09), GitHub API errors (GH-01 through GH-05), and rate limiting (RL-01 through RL-03). Key decisions proposed: FakeCopilotClient for integration tests (not StubCopilotClient pass-through), partial PR creation on partial failure, realistic 3-module workshop fixture, `.cs.txt` extension convention for code fixtures, trait-based E2E gating. Blocked on: orchestrator interface design, IPullRequestService definition, retry strategy decision (Polly vs custom), token budget tracker design. Decisions written to `.squad/decisions/inbox/kate-phase3-tests.md`.
