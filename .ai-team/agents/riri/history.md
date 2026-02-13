# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** GitHub App that attaches to workshop repos, analyzes issues requesting tech upgrades, uses Copilot to review content, updates code samples and prose, and delivers PRs with changes.
- **Stack:** C#, .NET, GitHub Apps, GitHub Copilot API, Webhooks
- **Created:** 2026-02-13

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-02-13): Copilot SDK for .NET will be used with custom SKILL.md prompts for content transformation and analysis; convention-based workshop detection with optional .workshop.yml manifest for edge cases — decided by Kamala

📌 Team update (2026-02-14): Release Notes and Dependabot Integration added to v1.1 — Trigger Classifier routes upgrade intent to issue parser, release notes parser, or Dependabot detector; both new triggers feed into same Upgrade Processor workflow with inferred intent. No changes to core Copilot integration or content analysis workflows — decided by Kamala
