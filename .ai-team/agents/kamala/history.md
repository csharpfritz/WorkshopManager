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
