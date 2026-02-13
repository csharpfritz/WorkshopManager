# Decisions

> Team decisions that all agents must respect. Managed by Scribe.

### 2026-02-13: Single-Project Architecture for Phase 1
**By:** Kamala
**What:** Single project `WorkshopManager.Api` contains all Phase 1 code. No `WorkshopManager.Core` extraction until Phase 2.
**Why:** Everything flows through the webhook endpoint at this stage. Premature abstraction adds ceremony without value. Split when Copilot integration and content analyzer grow heavier.
**Structure:**
```
WorkshopManager.sln
├── src/WorkshopManager.Api/
│   ├── Webhooks/
│   ├── Models/
│   ├── Services/
│   ├── Configuration/
│   └── Program.cs
└── tests/
    ├── WorkshopManager.UnitTests/
    └── WorkshopManager.IntegrationTests/
```
**Impact:** Simpler DI, simpler deployment, simpler debugging. Two test projects for CI separation (unit tests fast, integration tests boot full app).

### 2026-02-13: UpgradeIntent Data Model
**By:** Kamala, Riri
**What:** Immutable record representing parsed upgrade intent.
```csharp
public record UpgradeIntent(
    string SourceVersion,
    string TargetVersion,
    string Technology,
    UpgradeScope Scope,
    long IssueNumber,
    string IssueId,              // GitHub global node_id
    string RepoFullName,
    string RequestorLogin,       // Issue author
    string? ReleaseNotesUrl);

public enum UpgradeScope { Full, CodeOnly, DocsOnly, Incremental }
```
**Why:**
- `IssueId` needed for GraphQL operations (sub-issues, timeline events)
- `RequestorLogin` needed for @-mentions in PR description
- `ReleaseNotesUrl` scaffolds Phase 5 feature
- Immutable record ensures thread-safety
- Explicit enum avoids downstream string parsing
**Implementation Notes (Riri):**
- `UpgradeIntent.Empty()` sentinel factory for "no result" returns
- Field names: `SourceVersion`/`TargetVersion`, `FromVersion`/`ToVersion` (alignment with Copilot SDK)
- Versions default to `"current"` / `"latest"` when unparseable
**Impact:** Shared contract between America's webhook handler and Riri's parser. Kate uses for test assertions.

### 2026-02-13: IIssueParser Interface with Async Methods
**By:** Kamala, Riri
**What:**
```csharp
public interface IIssueParser
{
    Task<bool> IsWorkshopUpgradeRequestAsync(IssuesEvent issuesEvent);
    Task<UpgradeIntent> ParseAsync(IssuesEvent issuesEvent);
}
```
**Parsing strategy (WI-04):** Regex + keyword extraction. No LLM. Fast, deterministic, testable. Handles 80%+ of cases. LLM fallback deferred to Phase 2.
**Implementation Notes (Riri):**
- Uses `GeneratedRegex` source generators for zero-allocation regex
- Bot name detection from `GitHubApp:AppName` config (defaults to `workshop-manager[bot]`)
- Ordered technology keyword detection (`.NET` checked before `Node.js`)
- Scope parsing accepts multiple forms: `code-only`, `codeonly`, `code` all map to `CodeOnly`
- Release notes URL extraction: prefers `**Release Notes:**` structured field, falls back to URL pattern matching
**Why:** Async future-proofs for release notes fetch (Phase 5). Even though Phase 1 parsing is synchronous regex, interface contract prevents breaking change later.
**Impact:** America's webhook processor calls this. America ships stub, Riri swaps real implementation.

### 2026-02-13: Dual Trigger Mechanism
**By:** Kamala
**What:** WorkshopManager supports both label-based triggering (`workshop-upgrade` label) and direct assignment triggering (assign issue to bot). Labels trigger triage/analysis comments; assignment triggers actual upgrade work.
**Why:** Workshop authors need control over when the app starts consuming resources and making changes. Label-first allows review of the app's analysis before committing. Assignment-first enables confident authors to skip triage.

### 2026-02-13: Dual Trigger Precedence Rule
**By:** Kamala
**What:** If both `workshop-upgrade` label AND bot assignment exist on the same issue, assignment wins. Parser checks assignment first, then label.
**Why:** Design review edge case from Kate: which trigger takes precedence? Assignment is more explicit (author took action to assign) vs. label (could be accidental or exploratory). Explicit wins. This prevents duplicate processing.

### 2026-02-13: Webhook Handler Uses Octokit.Webhooks.AspNetCore
**By:** Kamala, America
**What:** 
```csharp
public class WorkshopWebhookEventProcessor : WebhookEventProcessor
{
    protected override async ValueTask ProcessIssuesWebhookAsync(...) { ... }
    protected override ValueTask ProcessPullRequestWebhookAsync(...) { ... }
}
```
**Implementation Notes (America):**
- Webhook endpoint at `/api/github/webhooks` via `MapGitHubWebhooks()`
- `Octokit.Webhooks` uses `ValueTask` (not `Task`) for processor overrides
- Webhook handler receives GitHub event, deserializes to `IssuesEvent`, passes to `IIssueParser.ParseAsync()`, queues async work, returns 200 immediately
- `WorkshopWebhookEventProcessor` overrides `ProcessIssuesWebhookAsync` (calls IIssueParser) and `ProcessPullRequestWebhookAsync` (Phase 5 stub)
- Options pattern: `GitHubAppOptions` with `ValidateOnStart()`. Webhook secret bridged from environment to `Octokit.Webhooks` integration
- Health check at `/healthz`
**Why:** Library handles signature validation, deserialization, routing correctly. Security-critical code should use well-tested libraries. Webhook handler focuses on routing, not parsing. Immediate 200 response prevents GitHub webhook timeouts. `UpgradeIntent` model captures all data needed for downstream upgrade processing.
**Impact:** America owns this. Routes to Riri's `IIssueParser`. Scaffolds `pull_request` event for Phase 5 Dependabot trigger.

### 2026-02-13: Webhook → Parser Contract
**By:** Kamala
**What:** Webhook handler receives GitHub event, deserializes to `IssuesEvent` (Octokit.Webhooks), passes to `IIssueParser.ParseAsync()`, queues async work, returns 200 immediately. Parser extracts `UpgradeIntent` model with target framework, packages, issue number, repo name.
**Why:** Design review established this as the interface between America's webhook endpoint and the issue parsing logic. Immediate 200 response prevents GitHub webhook timeouts. `UpgradeIntent` model captures all data needed for downstream upgrade processing.

### 2026-02-13: Webhook Error Handling Strategy
**By:** Kamala
**What:** Webhook failures: log and comment on issue ("Failed to process — @{author} please check logs"). Retry for transient failures (network, rate limit) only. Parsing failures = invalid request, no retry.
**Why:** Design review question from Kate: what happens when webhooks fail? This strategy balances user feedback (comment on issue) with system reliability (retry transient failures only). Parsing failures indicate bad input, not system failure — no point retrying.

### 2026-02-13: GitHub App Configuration Requirements
**By:** Kamala
**What:** GitHub App must request permissions: `issues:read`, `contents:write`, `pull_requests:write`, `metadata:read`. Subscribe to webhook events: `issues`, `issue_comment`, `label`. Use OAuth installation flow. Store webhook secret in environment variable.
**Why:** Design review surfaced these as minimum required permissions for WorkshopManager to function. Webhook secret must not be hardcoded (security requirement). OAuth flow is GitHub's recommended pattern for App installation.

### 2026-02-13: Configuration via Options Pattern + Environment Variables
**By:** Kamala
**What:**

| Setting | Location | Type |
|---------|----------|------|
| `GitHubApp:AppId`, `PrivateKey`, `WebhookSecret` | Env var | Secret |
| `GitHubApp:AppName` | appsettings.json | Config |
| `Copilot:ApiKey` | Env var | Secret |
| `Copilot:ApiEndpoint`, `Model`, `MaxTokens` | appsettings.json | Config |

**Options classes:** `GitHubAppOptions`, `CopilotSettings` with `[Required]` validation + `ValidateOnStart()`.

**Why:** Secrets never in source. Fail-fast on missing config. Options pattern is idiomatic .NET. Env var override (double underscore) works everywhere.
**Impact:** America sets up `GitHubAppOptions`. Riri sets up `CopilotSettings`. Both use DI.

### 2026-02-13: Copilot SDK for Content Transformation
**By:** Kamala, Riri
**What:** Use GitHub Copilot SDK for .NET with custom SKILL.md prompts rather than raw LLM API calls.
**Why:** The SDK provides a production-grade agent loop with file operations, response streaming, and tool orchestration. Custom skills teach Copilot workshop-specific transformation patterns. This avoids reinventing the agent infrastructure.

### 2026-02-13: Copilot SDK Integration Contract
**By:** Kamala, Riri
**What:** Copilot SDK integration exposes `ICopilotService.TransformContentAsync(filePath, content, UpgradeIntent)` interface. Must handle retries, rate limits, response streaming for large files. Must validate API token on initialization.
**Why:** Design review established this as the interface Riri builds for WI-05. Other agents will call this service to transform workshop content. Rate limit handling is critical (Copilot SDK docs are sparse on limits). Token validation prevents runtime failures.

### 2026-02-13: ICopilotClient Interface with Stub Implementation
**By:** Kamala, Riri
**What:**
```csharp
public interface ICopilotClient
{
    Task<CopilotResponse> TransformContentAsync(
        string content, string skillPromptPath, CopilotContext context, CancellationToken ct);
    Task<bool> ValidateConnectionAsync(CancellationToken ct);
}
```
**Phase 1 stub (Riri):** 
- `StubCopilotClient` returns input unchanged
- `CopilotResponse` with 4 fields: `TransformedContent`, `Success`, `ErrorMessage?`, `TokensUsed`
- `CopilotContext` with 5 fields: `RepositoryFullName`, `FilePath`, `FromVersion`, `ToVersion`, `Technology`
- Real SDK integration in Phase 2
**Why:** Stub lets Kate write integration tests without Copilot API credentials. Interface contract stable even if implementation swaps. Downstream services can reference `ICopilotClient` without blocking on SDK work.
**Impact:** Riri owns interface + stub. Swap to real client when Copilot SDK integrated.

### 2026-02-14: CopilotContext and CopilotResponse Field Alignment
**By:** America, Riri
**What:** 
```csharp
public record CopilotResponse(
    string TransformedContent,
    bool Success,
    string? ErrorMessage,
    int TokensUsed);

public record CopilotContext(
    string RepositoryFullName,
    string FilePath,
    string FromVersion,
    string ToVersion,
    string Technology);
```
**Why:** Field names and counts aligned with design review spec. Old fields `SourceVersion`/`TargetVersion` replaced with `FromVersion`/`ToVersion` to match Copilot SDK pattern. Added `TokensUsed` for rate limit tracking and `RepositoryFullName`/`FilePath` for file-level transforms.
**Impact:** 
- Riri: `StubCopilotClient` updated
- Kate: `CopilotResponse` now requires 4 constructor args
- `UpgradeIntent.Empty` sentinel added for "no result" returns
- Webhook Processor Note: `Octokit.Webhooks` uses `ValueTask` (not `Task`) for processor overrides

### 2026-02-13: Convention-Based Workshop Detection with Optional Manifest
**By:** Kamala
**What:** Auto-detect workshop structure from common directory conventions (`/src/`, `/docs/`, `*.csproj`, etc.) with optional `.workshop.yml` manifest for explicit configuration.
**Why:** Most workshops follow predictable patterns; requiring a manifest creates friction. But complex workshops or edge cases benefit from explicit configuration. Hybrid approach serves both.

### 2026-02-13: Multi-Commit PR Strategy
**By:** Kamala
**What:** Generate PRs with multiple logical commits (project files → code → docs → config) rather than a single monolithic commit.
**Why:** Easier code review — reviewers can focus on one category at a time. Enables partial cherry-picking if some changes are good but others need rework. Git history tells a clearer story.

### 2026-02-13: Content Type Boundaries (v1 Scope)
**By:** Kamala
**What:** v1 handles: C# code, project files, Markdown, JSON/YAML config, shell scripts. v1 does NOT handle: binary files, multimedia, Jupyter notebooks, complex migrations.
**Why:** Tight scope for v1 ensures we ship something useful quickly. Binary/multimedia files have no viable upgrade path. Notebooks have complex structure requiring separate investment. Scope can expand in v2.

### 2026-02-13: Parallel Development Mocking Strategy
**By:** Kamala
**What:** America writes webhook endpoint with mock parser (`return UpgradeIntent.Empty()`). Riri writes Copilot integration with mock transformer (`return content unchanged`). Kate writes tests against these mocks. Integration happens after all three streams land.
**Why:** Design review clarified how three parallel streams (WI-01, WI-03, WI-05) can proceed without blocking on each other. Mocks enable independent development and testing. Integration risk is minimized because interfaces are agreed upon upfront (ceremony documented contracts).

### 2026-02-13: Testing Strategy — xUnit + WebApplicationFactory + Replay Fixtures
**By:** Kamala, Kate
**What:**

**Unit tests (WI-06):**
- xUnit + NSubstitute + FluentAssertions
- 12 markdown fixture files for parser logic
- Focus: pure parsing, no HTTP, no GitHub API

**Integration tests (WI-07):**
- WebApplicationFactory + DI overrides
- 8 webhook JSON payloads + HMAC signature tests
- Focus: signature validation, routing, end-to-end flow
- Mocks: `IIssueParser`, `ICopilotClient`, work scheduler

**Test infrastructure (Kate builds):**
- `HmacSignatureHelper`, `WebhookTestClient`, `IssueEventBuilder`
- `UpgradeIntentAssertions`, `TestFixtureLoader`, `CustomWebApplicationFactory<T>`

**Why:** xUnit ecosystem standard. WebApplicationFactory tests real ASP.NET pipeline without server. Replay fixtures match real GitHub behavior. Signature validation is security-critical.
**Impact:** Kate blocked on America's `Program.cs` (WI-07) and Riri's `UpgradeIntent` types (WI-06).

### 2026-02-14: Test Fixture Loading via File System
**By:** Kate
**What:** Test fixtures (`.md` and `.json`) are loaded from disk via `TestFixtureLoader` using file-system paths, with `CopyToOutputDirectory: PreserveNewest` in `.csproj`. Not using embedded resources.
**Why:** File-system loading is simpler to debug (you can `cat` the file), easier to update (no rebuild needed to see fixture changes), and avoids embedded resource naming convention issues. The `TestFixtureLoader.GetProjectDirectory()` walks up from `AppContext.BaseDirectory` to find the `.csproj`, making it work in both `dotnet test` and IDE test runners.
**Impact:** Both test `.csproj` files now include `<None Update="Fixtures\**\*"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></None>`. New fixtures just need to be dropped in `Fixtures/` — no `.csproj` edits needed.

### 2026-02-14: Markdown Fixtures Use Title-First Convention
**By:** Kate
**What:** Markdown issue fixtures put the issue title on line 1, followed by a blank line, then the body. `TestFixtureLoader.LoadTitle()` and `LoadBody()` split on first newline.
**Why:** GitHub issues have separate title and body fields. For unit tests, we sometimes need just the title (e.g., `title-upgrade-from-to.md`) and sometimes just the body (e.g., `body-structured-full.md`). Having a consistent convention means the loader can split them predictably.

### 2026-02-14: WebhookTestClient Provides Three Signature Modes
**By:** Kate
**What:** `WebhookTestClient` has three send methods: `SendWebhookAsync` (valid signature), `SendWebhookWithInvalidSignatureAsync` (wrong hash, correct format), `SendWebhookWithoutSignatureAsync` (no header). `HmacSignatureHelper` also provides `CreateMalformedSignature()` (wrong prefix).
**Why:** Signature validation is security-critical. We need to test: valid signature passes, wrong hash is rejected, missing header is rejected, wrong prefix (sha1 vs sha256) is rejected. Four distinct failure modes, three convenience methods.

### 2026-02-14: Release Notes and Dependabot Integration
**By:** Kamala
**What:** WorkshopManager will support two new trigger/discovery mechanisms: (1) Release Notes Link Trigger — fetch and parse release notes URLs to infer upgrade scope and proceed with standard upgrade workflow; (2) Dependabot Integration — detect Dependabot PRs and create companion PRs updating workshop content to stay in sync with dependency changes.
**Why:** Release notes trigger reduces friction (authors paste a link, app figures out what changed) and aligns with existing issue-based trigger model. Dependabot integration keeps workshop prose in sync with code dependency versions — without it, readers see instructions for npm v18 while code uses npm v20. Both features fit cleanly into existing architecture: Trigger Classifier routes to appropriate handler, both ultimately call Upgrade Processor with inferred intent. Zero breaking changes to existing workflows. GitHub App webhook expanded to include `pull_request` event. Configuration is additive.
**Architectural Impact:**
- New components: Trigger Classifier, Release Notes Fetcher/Parser, Dependabot PR Detector
- Minimal — fits into existing dual-trigger + Upgrade Processor architecture
- Zero breaking changes — existing label/assignment triggers unchanged
- Configuration additive — new `release_notes` and `dependabot` sections in `.github/workshop-manager.yml`
**Work Estimate:** Phase 5 — 13 items, ~30 story points (WI-26 to WI-38)

### 2026-02-14: IssueParser regex bug — multi-word version strings
**By:** Kate (Tester)
**Status:** Documented (not fixed — outside Tester scope)
**What:** Version extraction regex patterns fail on multi-word values. Example: `**To:** .NET 9` captures only `.NET` instead of `.NET 9`.
**Affected patterns:**
- `BodyToFieldPattern`: `\*\*To:\*\*\s*v?(\S+)` 
- `BodyFromFieldPattern`: `\*\*From:\*\*\s*v?(\S+)`
- `TitleFromToPattern`: `(?:upgrade|update|migrate)\s+from\s+v?(\S+)\s+to\s+v?(\S+)`
**Why:** `\S+` captures only the first non-whitespace token. Multi-word technology names (e.g., `.NET 9`, `Node.js 20`) are truncated.
**Impact:** All fixtures with `.NET X` or similar multi-word versions return truncated strings. Unit tests document this behavior with `// BUG:` comments, asserting actual vs. expected per team protocol.
**Recommendation:** Change capture group to `(\S+(?:\s+\S+)?)` or use anchored pattern like `(.+?)` to capture full version string.
