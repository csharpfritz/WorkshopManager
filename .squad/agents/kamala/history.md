# Project Context

- **Owner:** Jeffrey T. Fritz
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

### 2026-02-14: Workshop Structure Detection Design (WI-08)

**Key Files:**
- `docs/design/workshop-structure.md` — Full design document for implementers
- `.ai-team/decisions/inbox/kamala-workshop-structure-design.md` — Decision record

**Architecture Decisions:**
1. **Two-strategy detection** — Manifest-based (`.workshop.yml`) checked first, convention-based as fallback. Hybrid strategy when manifest is partial.
2. **Remote-first analysis** — `IWorkshopAnalyzer.AnalyzeAsync` takes `repoFullName` + `commitSha`, not local path. All file access via `IRepositoryContentProvider` abstraction backed by GitHub Git Tree API.
3. **Content classification model** — `WorkshopStructure` (top-level) contains `ContentItem` entries, each classified as `CodeSample`, `Documentation`, `ProjectFile`, `Configuration`, or `Asset`. Items carry `VersionReference` and `DependencyReference` for downstream transformation.
4. **Convention detection ordered by specificity** — Technology detection: `.csproj` → `package.json` → `pyproject.toml` → `go.mod` → `pom.xml`. First match wins for primary technology.
5. **IRepositoryContentProvider abstraction** — Decouples analyzer from Octokit, enables `InMemoryContentProvider` for testing.
6. **Manifest schema** — `.workshop.yml` with all-optional fields: `name`, `technology` (primary + version), `structure` (modules, shared, exclude). Gaps filled by convention detection.

**Routing:**
- Riri: Implements `WorkshopAnalyzer` (WI-09), `ManifestParser` (WI-10)
- America: Implements `IRepositoryContentProvider` backed by Octokit
- Kate: Tests with `InMemoryContentProvider` fixture trees (WI-13)

**Design rationale for remote-first:** Phase 2 reads repo contents via GitHub API. Pinning to commit SHA prevents TOCTOU issues. Aligns with PR generation which is also API-based. If WI-18 (build validation) needs local filesystem, that's a separate service with its own interface.

### 2026-02-14: WI-11 — Copilot Skill Prompts Designed

**Deliverables:**
- Four skill prompt templates in `src/WorkshopManager.Api/Skills/`:
  - `upgrade-code-sample.md` — Code transformation preserving pedagogical intent
  - `upgrade-documentation.md` — Documentation transformation preserving teaching flow
  - `upgrade-project-file.md` — Project/config file precise version upgrades
  - `analyze-breaking-changes.md` — Breaking change analysis returning structured JSON
- `ISkillResolver` interface + `SkillResolver` implementation in Services/
- `ContentItemType` enum in Models/ (CodeSample, Documentation, ProjectFile, Configuration)
- Decision document: `.ai-team/decisions/inbox/kamala-copilot-skills-design.md`

**Key Design Decisions:**
1. **Four placeholders** — `{{technology}}`, `{{fromVersion}}`, `{{toVersion}}`, `{{releaseNotesUrl}}` map directly to existing `CopilotContext` and `UpgradeIntent` fields. No new data types needed.
2. **ICopilotClient unchanged** — The `skillPromptPath` parameter already accommodates the skill design. Real client loads template, hydrates placeholders, sends to API. Zero breaking changes.
3. **Skill routing via ISkillResolver** — Maps `(ContentItemType, UpgradeScope)` to skill file path. `Incremental` scope routes to analysis skill regardless of content type. Configuration reuses project-file skill.
4. **REVIEW markers in output** — Skills instruct Copilot to insert `// REVIEW:` or `<!-- REVIEW: -->` for items needing human attention, integrating with PR review workflow.
5. **Skills as files, not embedded** — Markdown files on disk, not embedded resources or hardcoded strings. Easier iteration, editable without recompilation.

**Impact on other agents:**
- Riri: Consumes `ISkillResolver` in WI-12 to route content to skills
- Kate: Should test `SkillResolver` routing (4 content types × 4 scopes = 16 combinations)
- America: No impact — skills internal to Copilot pipeline

### 2026-02-22: Phase 3 Transformation & PR Design (WI-14)

**Key Files:**
- `docs/design/phase3-transformation-pr.md` — Full design document for Phase 3 implementers
- `.squad/decisions/inbox/kamala-phase3-design.md` — Decision record (7 decisions)

**Architecture Decisions:**
1. **Separate code and docs transformation services** — `ICodeTransformationService` (code, project files, config) and `IDocumentationTransformationService` (markdown/prose). Different Copilot prompt strategies; different failure severity; separate mocks for testing.
2. **Per-file TransformationResult** — Each file produces its own result with Success/Failure, content, error, token count. `TransformationSummary` aggregates. Enables partial success PRs.
3. **Sequential Copilot calls** — Not parallel. Rate limits unknown. Sequential is safe. Parallelism deferred to production observation.
4. **Git Data API for commits** — Blobs → trees → commits → refs. No local clone. Remote-first. Multi-commit branches without push.
5. **Branch recreate on retry** — Delete and recreate from current HEAD. No merge commits on automation-owned branches.
6. **Failed files excluded from PR** — Only successfully transformed files committed. Failures listed in PR description table.
7. **UpgradeOrchestrator** — Single entry point (`IUpgradeOrchestrator.ExecuteAsync`) orchestrates full pipeline: intent → analysis → transformation → PR. Webhook handler stays thin.

**New Models:** `TransformationResult`, `TransformationSummary`, `PullRequestResult`, `UpgradeResult`, `UpgradePhase`
**New Interfaces:** `ICodeTransformationService`, `IDocumentationTransformationService`, `IPullRequestService`, `IUpgradeOrchestrator`

**DI:** All Phase 3 services registered as `Scoped` to share `GitHubContentProvider` auth token cache per webhook scope.

**Team Routing:**
- Riri: WI-15 (`CodeTransformationService`), WI-16 (`DocumentationTransformationService`)
- America: WI-17 (`PullRequestService`) — Git Data API, commit strategy, PR template
- Kate: WI-19 (E2E tests) — Mock all four interfaces, use `UpgradeOrchestrator` as test entry point
- Kamala: WI-22 (code review against this design)

**Open Questions:**
- Should docs transformer include related code files as Copilot context? (Token budget TBD by Riri)
- PR description truncation for large workshops? (GitHub limit: 65536 chars)
- Build validation (WI-18): proposed to run after draft PR creation, not before
