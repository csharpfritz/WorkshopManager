# Design Review — Phase 1 Foundation Kickoff — 2026-02-13

**Facilitator:** Kamala
**Participants:** America (Frontend Dev), Riri (Backend Dev), Kate (Tester)
**Context:** Starting Phase 1 with 7 work items, focusing on three independent streams that can start immediately:
- WI-01: America sets up GitHub App registration and webhook endpoint
- WI-03: Kamala designs issue parsing logic  
- WI-05: Riri sets up Copilot SDK integration scaffold

## Decisions

### GitHub App Configuration Requirements
- App must request permissions: `issues:read`, `contents:write`, `pull_requests:write`, `metadata:read`
- Webhook events to subscribe: `issues`, `issue_comment`, `label`
- App installation will use GitHub's App installation flow (OAuth)
- Webhook secret must be stored securely (environment variable, not hardcoded)

### Webhook Endpoint Contract
- POST endpoint at `/webhooks/github`
- Returns 200 OK immediately (processing happens async)
- Validates webhook signature before processing (security requirement)
- Uses `Octokit.Webhooks.AspNetCore` for handling (per existing team decision)

### Issue Parser Interface
The issue parser must expose:
```csharp
interface IIssueParser {
  UpgradeIntent ParseIssue(Issue issue);
  bool IsWorkshopUpgradeRequest(Issue issue);
}

class UpgradeIntent {
  string TargetFramework;  // e.g., "net9.0"
  List<string> PackagesToUpgrade;
  string IssueNumber;
  string RepoFullName;
}
```

### Copilot SDK Integration Contract
Riri's scaffold must provide:
```csharp
interface ICopilotService {
  Task<TransformResult> TransformContentAsync(
    string filePath, 
    string content, 
    UpgradeIntent intent
  );
}
```
- Must handle retries and rate limits
- Must stream responses for large files
- Must validate Copilot API token on initialization

## Action Items

| Owner | Action |
|-------|--------|
| America | Set up GitHub App registration with required permissions; implement webhook endpoint with signature validation; return 200 immediately |
| Kamala | Design `IIssueParser` interface and `UpgradeIntent` model; document parsing rules for label-based vs assignment-based triggers |
| Riri | Scaffold Copilot SDK integration with `ICopilotService` interface; implement token validation and basic error handling |
| Kate | Prepare test cases for webhook signature validation; prepare mock issue payloads for parser testing |

## Risks & Concerns

### From America (Frontend/GitHub App)
- **Risk:** Webhook signature validation failures in production — need test harness with real GitHub signatures
- **Interface requirement:** Need clear contract for what the webhook handler passes to downstream processors (raw payload vs. parsed object?)
- **Blocker:** GitHub App must be registered BEFORE local webhook testing can start — need ngrok or similar tunnel

### From Riri (Backend/Copilot SDK)
- **Risk:** Copilot SDK rate limits not well documented — may need circuit breaker pattern early
- **Interface requirement:** Need to know what file types come in (America's parser should filter unsuitable files before calling Copilot)
- **Concern:** Large workshops (100+ files) could cause timeout issues — need async processing with progress tracking

### From Kate (Tester)
- **Risk:** No clear error handling strategy for webhook failures — should we retry? Log and skip? Surface to issue comments?
- **Testing blocker:** Can't write integration tests until webhook endpoint exists and parser interface is defined
- **Edge case:** What happens if someone labels an issue `workshop-upgrade` but it's already assigned to the bot? Which trigger wins?

## Resolutions

### Webhook → Parser Contract
**Decision:** Webhook handler passes deserialized `IssuesEvent` object (from Octokit.Webhooks) to parser. Parser extracts intent, handler queues async work. Webhook returns 200 immediately.

### Error Handling Strategy
**Decision:** Webhook failures get logged and commented on the issue: "Failed to process — @{author} please check logs." Retries for transient failures (network, rate limit) only. Parsing failures = invalid request, no retry.

### Dual Trigger Precedence
**Decision:** If both label and assignment exist, assignment wins (it's more explicit). Parser checks assignment first, then label.

### Testing Before Full Integration
**Decision:** America writes webhook endpoint with a mock parser (`return UpgradeIntent.Empty()`). Riri writes Copilot integration with a mock transformer (`return content unchanged`). Kate writes tests against these mocks. Integration happens after all three streams land.

## Next Steps

1. **All three streams (WI-01, WI-03, WI-05) are greenlit to start** — no blockers identified
2. **Interface contracts documented** — each agent knows what they're building toward
3. **Mocking strategy established** — enables parallel work without integration dependencies
4. **Error handling clarified** — reduces rework risk

**Ceremony duration:** ~15 minutes (focused)
**Outcome:** ✅ Ready to ship — all three work items can proceed independently

---

# Design Review: Phase 1 Foundation — Detailed Technical Contracts
**Date:** 2026-02-13 (Follow-up session)
**Facilitator:** Kamala
**Participants:** America (Frontend Dev), Riri (Backend Dev), Kate (Tester)

## Context

Phase 1 establishes the core GitHub App infrastructure with 7 work items:
- **WI-01/02:** GitHub App registration, webhook endpoint, signature validation (America)
- **WI-03/04:** Issue parsing logic design and service implementation (Kamala design, Riri implementation)
- **WI-05:** Copilot SDK integration scaffold (Riri)
- **WI-06/07:** Unit tests (issue parser) and integration tests (webhook handler) (Kate)

This session resolves specific technical contracts, data models, and testing infrastructure to enable parallel implementation across three agents.

---

## Decisions

### 1. Solution Structure

**Decision:** Single-project architecture for Phase 1.

```
WorkshopManager.sln
├── src/
│   └── WorkshopManager.Api/
│       ├── Webhooks/                  # Event processors, routing
│       ├── Models/                    # UpgradeIntent, DTOs
│       ├── Services/                  # IIssueParser, ICopilotClient
│       ├── Configuration/            # Options classes
│       └── Program.cs
└── tests/
    ├── WorkshopManager.UnitTests/    # Parser logic, helpers
    └── WorkshopManager.IntegrationTests/  # Webhook pipeline, WebApplicationFactory
```

**Rationale:** Premature abstraction adds ceremony without value. Everything flows through the webhook endpoint at this stage. We'll extract `WorkshopManager.Core` in Phase 2 when the Copilot integration and content analyzer grow heavier.

**Test project split:** Two separate projects for clear CI separation — unit tests run fast, integration tests boot the full app.

**Namespace convention:** `WorkshopManager.*` (e.g., `WorkshopManager.Models`, `WorkshopManager.Services`).

---

### 2. UpgradeIntent Model

**Decision:** Shared data contract representing parsed upgrade intent.

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

public enum UpgradeScope
{
    Full,
    CodeOnly,
    DocsOnly,
    Incremental
}
```

**Key fields:**
- `IssueId`: Global node_id for GraphQL operations (sub-issues, timeline events)
- `RequestorLogin`: Issue author for @-mentions in PR description
- `ReleaseNotesUrl`: Optional link to release notes (Phase 5 feature, scaffold now)

**Empty sentinel:**
```csharp
public static UpgradeIntent Empty { get; } = new(...); // All defaults
```

**Rationale:** Immutable record type ensures thread-safety. Explicit enum for scope avoids string parsing downstream. `IssueId` + `IssueNumber` duality matches GitHub's dual identifier system (GraphQL vs REST).

---

### 3. IIssueParser Interface

**Decision:** Contract for issue parsing with classification and extraction methods.

```csharp
public interface IIssueParser
{
    /// <summary>
    /// Checks if issue represents a workshop upgrade request.
    /// Returns true if: labeled "workshop-upgrade" OR assigned to bot.
    /// </summary>
    Task<bool> IsWorkshopUpgradeRequestAsync(IssuesEvent issuesEvent);

    /// <summary>
    /// Extracts upgrade intent from issue title/body.
    /// Returns UpgradeIntent.Empty if parsing fails.
    /// </summary>
    Task<UpgradeIntent> ParseAsync(IssuesEvent issuesEvent);
}
```

**Parsing strategy (WI-04):**
- **Phase 1:** Regex + keyword extraction (no LLM). Fast, deterministic, testable.
- **Title parsing:** `"Upgrade from .NET 8 to .NET 9"` → regex capture groups
- **Body structured fields:** `**From:**`, `**To:**`, `**Scope:**`, `**Release Notes:**` markdown patterns
- **Scope keywords:** `"code-only"` → `UpgradeScope.CodeOnly`, etc.
- **URL detection:** Heuristic regex for release notes links
- **Fallbacks:** No source version → `"current"` (inferred from repo); no scope → `Full`

**Rationale:** Async interface future-proofs for release notes fetch (Phase 5). Regex handles 80%+ of cases without external dependency. LLM fallback deferred to Phase 2.

---

### 4. Webhook Handler Contract

**Decision:** Use `Octokit.Webhooks.AspNetCore` library with custom event processor.

```csharp
public class WorkshopWebhookEventProcessor : WebhookEventProcessor
{
    protected override async Task ProcessIssuesWebhookAsync(
        WebhookHeaders headers,
        IssuesEvent issuesEvent,
        IssuesAction action)
    {
        if (!await _issueParser.IsWorkshopUpgradeRequestAsync(issuesEvent))
            return;  // Ignore non-upgrade issues

        var intent = await _issueParser.ParseAsync(issuesEvent);
        // Enqueue work, post comment, etc.
    }

    protected override Task ProcessPullRequestWebhookAsync(
        WebhookHeaders headers,
        PullRequestEvent prEvent,
        PullRequestAction action)
    {
        // Dependabot trigger (Phase 5) — log and return 200 for now
        return Task.CompletedTask;
    }
}
```

**Integration point:** America's webhook processor calls Riri's `IIssueParser`. America ships with a no-op stub; Riri swaps in the real implementation.

**Rationale:** `Octokit.Webhooks.AspNetCore` handles signature validation, deserialization, and routing correctly. Security-critical code should use well-tested libraries. Webhook handler focuses on routing, not parsing.

---

### 5. Copilot SDK Scaffold

**Decision:** Interface-first design with stub implementation in Phase 1.

```csharp
public interface ICopilotClient
{
    Task<CopilotResponse> TransformContentAsync(
        string content,
        string skillPromptPath,
        CopilotContext context,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default);
}

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

**Phase 1 implementation:** Stub that returns input unchanged. Real SDK integration in WI-05 (Phase 2).

**Rationale:** Stub lets Kate write integration tests without Copilot API credentials. Interface contract is stable even if implementation swaps. Downstream services (content transformer) can reference `ICopilotClient` without blocking on SDK work.

---

### 6. Configuration Strategy

**Decision:** Options pattern + environment variable overrides.

| Setting | Location | Type | Example |
|---------|----------|------|---------|
| `GitHubApp:AppId` | Env var | Secret | `GITHUB_APP_ID=123456` |
| `GitHubApp:PrivateKey` | Env var | Secret | `GITHUB_APP_PRIVATE_KEY=-----BEGIN...` |
| `GitHubApp:WebhookSecret` | Env var | Secret | `GITHUB_WEBHOOK_SECRET=abc123` |
| `GitHubApp:AppName` | appsettings.json | Config | `"workshop-manager"` |
| `Copilot:ApiEndpoint` | appsettings.json | Config | `"https://api.githubcopilot.com"` |
| `Copilot:ApiKey` | Env var | Secret | `Copilot__ApiKey=sk-...` |
| `Copilot:Model` | appsettings.json | Config | `"claude-sonnet-4"` |

**Options classes:**
```csharp
public class GitHubAppOptions
{
    public const string SectionName = "GitHubApp";
    [Required] public string AppId { get; set; } = default!;
    [Required] public string PrivateKey { get; set; } = default!;
    [Required] public string WebhookSecret { get; set; } = default!;
    public string AppName { get; set; } = "workshop-manager";
}

public class CopilotSettings
{
    public const string SectionName = "Copilot";
    [Required] public string ApiEndpoint { get; set; } = "https://api.githubcopilot.com";
    [Required] public string ApiKey { get; set; } = default!;
    public string Model { get; set; } = "claude-sonnet-4";
    public int MaxTokens { get; set; } = 4096;
    public int TimeoutSeconds { get; set; } = 120;
}
```

**DI registration:**
```csharp
builder.Services
    .AddOptions<GitHubAppOptions>()
    .BindConfiguration(GitHubAppOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<CopilotSettings>()
    .BindConfiguration(CopilotSettings.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**Rationale:** Secrets never in source. `ValidateOnStart()` fails fast if required config missing. Options pattern is idiomatic .NET. Environment variable override (double underscore) works everywhere (local, CI, Azure App Service).

---

### 7. Testing Strategy

**Framework:** xUnit + NSubstitute + FluentAssertions

**Unit tests (WI-06):** Issue parser logic
- **Test data:** 12 markdown fixture files (`.md`) covering happy path, edge cases, structured fields, URL detection
- **Fixtures:** `title-upgrade-from-to.md`, `body-structured-full.md`, `body-garbage-markdown.md`, etc.
- **Focus:** Pure parsing logic — no HTTP, no GitHub API
- **Mocks:** None (parser takes string inputs)

**Integration tests (WI-07):** Webhook handler pipeline
- **Test host:** `WebApplicationFactory<Program>` + custom DI overrides
- **Test data:** 8 webhook JSON payloads + HMAC signature tests
- **Fixtures:** `webhook-issues-labeled.json`, `webhook-issues-assigned-bot.json`, `webhook-malformed.json`, etc.
- **Focus:** Signature validation, routing, end-to-end flow
- **Mocks:** `IIssueParser`, `ICopilotClient`, work scheduler

**Test infrastructure (Kate builds):**
- `HmacSignatureHelper`: Computes valid/invalid `X-Hub-Signature-256` headers
- `WebhookTestClient`: Wraps `HttpClient` with GitHub webhook headers
- `IssueEventBuilder`: Fluent builder for `IssuesEvent` objects
- `UpgradeIntentAssertions`: Custom FluentAssertions extensions
- `TestFixtureLoader`: Reads embedded `.md` / `.json` files
- `CustomWebApplicationFactory<T>`: Base class with DI overrides

**Rationale:** xUnit is ecosystem standard. `WebApplicationFactory` tests the real ASP.NET pipeline without spinning up a server. Replay fixtures ensure tests match real GitHub behavior. Signature validation is security-critical and must be tested.

---

## Open Items

### For Jeffrey (PRD owner)
1. **Copilot SDK NuGet package name** — Confirm package: `GitHub.Copilot.SDK`? Need exact identifier for WI-05.
2. **Webhook endpoint route** — Confirm `/api/github/webhooks` or `/webhooks/github`?

### For Phase 2
1. **LLM fallback for issue parsing** — If regex blocks >20% of valid issues, escalate to Copilot-based parsing.
2. **`WorkshopManager.Core` extraction** — When content analyzer and transformation services grow, split into Core project.

---

## Dependencies and Work Order

**Parallel tracks:**

**Track 1 (America):**
- WI-01: GitHub App registration, webhook endpoint setup
- WI-02: Webhook signature validation + `WorkshopWebhookEventProcessor` with stub `IIssueParser`

**Track 2 (Riri):**
- WI-04: Real `IssueParser` implementation (depends on America's `IssuesEvent` routing)
- WI-05: `ICopilotClient` stub → real implementation

**Track 3 (Kate):**
- WI-06: Unit tests (blocks on Riri's `IIssueParser` + `UpgradeIntent` types)
- WI-07: Integration tests (blocks on America's `Program.cs` + webhook config)

**Critical path:**
1. America ships `Program.cs` + stub `IIssueParser` interface (blocks Kate WI-07)
2. Riri ships `UpgradeIntent` model + `IIssueParser` interface (blocks Kate WI-06)
3. Kate builds test infrastructure
4. Riri ships real `IIssueParser` implementation → Kate validates with WI-06
5. Kate ships WI-07 integration tests → validates end-to-end webhook flow

---

## Summary

**Contracts established:**
- Single-project architecture (split in Phase 2)
- `UpgradeIntent` record with 9 fields (includes `IssueId`, `RequestorLogin`)
- `IIssueParser` interface with async methods, regex-based parsing
- Webhook handler uses `Octokit.Webhooks.AspNetCore`, routes to `IIssueParser`
- `ICopilotClient` interface with stub implementation
- Options pattern for configuration, env vars for secrets
- xUnit + WebApplicationFactory + webhook replay fixtures

**Open questions:**
- Copilot SDK package name (needs Jeffrey confirmation)
- Webhook endpoint route confirmation (needs Jeffrey confirmation)

**Next steps:**
- America: Create solution structure, ship `Program.cs` + stubs
- Riri: Ship `UpgradeIntent` + `IIssueParser` types (stub OK, unblocks Kate)
- Kate: Build test infrastructure + fixture files
- All: Implement work items in parallel

**Review complete.** All contracts and interfaces defined. Team can proceed with Phase 1 implementation.
