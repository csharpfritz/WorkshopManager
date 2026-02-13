# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
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
