# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** GitHub App that attaches to workshop repos, analyzes issues requesting tech upgrades, uses Copilot to review content, updates code samples and prose, and delivers PRs with changes.
- **Stack:** C#, .NET, GitHub Apps, GitHub Copilot API, Webhooks
- **Created:** 2026-02-13

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-02-13): Webhook handling will use Octokit.Webhooks.AspNetCore for signature validation and event routing; PRs will use multi-commit strategy by category for easier review; dual trigger mechanism supports both label and assignment workflows — decided by Kamala

📌 Team update (2026-02-14): Release Notes and Dependabot Integration added to v1.1 — app now detects release notes URLs in issues (fetches and parses to infer scope) and detects Dependabot PRs to create companion PRs with workshop updates. GitHub App webhook expanded to include `pull_request` event. Trigger Classifier routes to appropriate handler. Configuration expanded with `release_notes` and `dependabot` sections in `.github/workshop-manager.yml` — decided by Kamala
