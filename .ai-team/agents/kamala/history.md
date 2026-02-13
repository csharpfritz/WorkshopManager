# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** GitHub App that attaches to workshop repos, analyzes issues requesting tech upgrades, uses Copilot to review content, updates code samples and prose, and delivers PRs with changes.
- **Stack:** C#, .NET, GitHub Apps, GitHub Copilot API, Webhooks
- **Created:** 2026-02-13

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-02-13: PRD and Architecture Established

**Key Files:**
- `docs/PRD.md` — Comprehensive Product Requirements Document with architecture, workflows, and work breakdown

**Architecture Decisions:**
1. **Dual trigger mechanism** — Label-based (`workshop-upgrade`) for triage, assignment-based for direct action. This gives authors control over when work begins.
2. **Copilot SDK integration** — Using GitHub Copilot SDK for .NET to perform intelligent content analysis and transformation, with custom SKILL.md prompts.
3. **Convention-over-configuration** — Workshop structure auto-detected from common patterns (`/src/`, `/docs/`, `*.csproj`), with optional `.workshop.yml` manifest for explicit control.
4. **Multi-commit PR strategy** — Logical commits by category (project files, code, docs) rather than single monolithic commit. Enables partial cherry-picking and easier review.
5. **Content type boundaries** — v1 handles code files, project files, markdown, and config files. Binary/multimedia files are explicitly out of scope.

**Tech Stack:**
- .NET 9 + ASP.NET Core Minimal APIs
- Octokit.net + Octokit.Webhooks.AspNetCore for GitHub integration
- GitHub Copilot SDK for .NET for AI-powered analysis
- Azure App Service or Functions for hosting

**Team Routing:**
- America: Webhook handlers, GitHub App surfaces, PR generation
- Riri: Copilot integration, content analysis, transformation services
- Kate: Testing across all layers
- Kamala: Architecture, design reviews, scope decisions

**Open Questions for Jeffrey:**
- Default trigger mechanism preference
- Copilot model preference (Claude vs GPT-5)
- Build validation requirements
- Hosting preference (App Service vs Functions)

### 2026-02-14: Release Notes & Dependabot Integration Added to v1.1

**Rationale:**
Both release notes and Dependabot integrate into the existing dual-trigger + workflow model without major rework. They extend *how* the app discovers upgrade intent, not the core architecture.

**Release Notes Link Trigger:**
- New entry-point: when an issue contains a URL to release notes, app fetches and parses them
- Parsing extracts version info and API changes to infer upgrade scope
- Feeds the same Upgrade Processor workflow as issue-based requests
- Reduces friction: authors don't need to manually summarize "what changed"
- New components: Release Notes Fetcher/Parser, Trigger Classifier

**Dependabot Integration Trigger:**
- New entry-point: when Dependabot opens a PR, WorkshopManager detects it via pull_request webhook
- Extractor infers package/version info from Dependabot's commit messages and branch name
- Creates a companion PR with content updates alongside code dependency changes
- Keeps workshop instructions in sync with actual dependency versions
- New components: Dependabot PR Detector, Companion PR workflow

**Architecture notes:**
- Trigger Classifier replaces direct issue parsing — now routes to: issue parser, release notes parser, or Dependabot detector
- Both new triggers ultimately call Upgrade Processor with inferred intent
- Configuration expanded: release_notes section (enable/disable, auto-detect) and dependabot section (enable/disable, targeted triggers)
- GitHub App webhook events expanded to include `pull_request` for Dependabot detection

**Work items:** 13 new items (WI-26 to WI-38) split across phases 5 and beyond, estimated at 30 story points total
