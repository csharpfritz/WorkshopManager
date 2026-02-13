# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** GitHub App that attaches to workshop repos, analyzes issues requesting tech upgrades, uses Copilot to review content, updates code samples and prose, and delivers PRs with changes.
- **Stack:** C#, .NET, GitHub Apps, GitHub Copilot API, Webhooks
- **Created:** 2026-02-13

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-02-13): Copilot SDK for .NET will be used with custom SKILL.md prompts for content transformation and analysis; convention-based workshop detection with optional .workshop.yml manifest for edge cases — decided by Kamala

📌 Team update (2026-02-13): ICopilotClient interface with stub implementation — Phase 1 stub returns content unchanged; real SDK integration in Phase 2; interface contract stable regardless of implementation; CopilotResponse (4 fields) and CopilotContext (5 fields) records established — decided by Kamala

📌 Team update (2026-02-14): Release Notes and Dependabot Integration extends existing architecture — Trigger Classifier routes upgrade intent; both new triggers call Upgrade Processor; no changes to core Copilot integration or content analysis workflows; Phase 5 feature (WI-26 to WI-38) — decided by Kamala

📌 WI-04/05 shipped (2026-02-14): Shared models and interfaces complete — UpgradeIntent, CopilotResponse, CopilotContext records; IIssueParser, ICopilotClient interfaces; IssueParser regex implementation with GeneratedRegex source generators; StubCopilotClient pass-through; bot name detection from config; technology/scope/release notes parsing — decided by Riri
