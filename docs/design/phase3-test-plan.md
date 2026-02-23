# Phase 3 Test Plan — Transformation & PR Generation

> **Author:** Kate (Tester)  
> **Date:** 2026-02-22  
> **Status:** Draft — architecture still being finalized  
> **Covers:** PRD §5 (Copilot Integration), §6 (Content Analysis), §7 (PR Generation)

---

## 1. Scope

Phase 3 introduces three major capabilities:

1. **Code Transformation** — Copilot-powered transformation of C# code, project files, scripts
2. **Docs Transformation** — Copilot-powered transformation of Markdown prose and config files
3. **PR Generation** — Branch creation, multi-commit strategy, PR body generation, labels

This plan covers unit, integration, and end-to-end test scenarios for all three. It does **not** cover the content discovery pipeline (Phase 2 — already tested with 89+ tests).

---

## 2. Test Categories

### 2.1 Unit Tests

Isolated tests with no external dependencies. All Copilot API calls mocked via `NSubstitute`. All GitHub API calls mocked. File system interactions use `InMemoryContentProvider`.

**Target services:**
- `CopilotClient` (skill loading, placeholder hydration, response parsing)
- `SkillResolver` (routing content types to skill files)
- Transformation orchestrator (batching, ordering, error accumulation)
- PR body builder (Markdown generation, change summary)
- Branch name generator (slug formatting, length limits)

### 2.2 Integration Tests

Test real service interactions within the ASP.NET pipeline using `WebApplicationFactory` with DI overrides. Copilot API replaced with a `FakeCopilotClient` that returns deterministic transformed content (not the pass-through `StubCopilotClient`).

**Target flows:**
- Webhook → Parse → Analyze → Transform → PR (end-to-end with fakes)
- Transformation orchestrator → CopilotClient batching behavior
- PR creation flow → GitHub API contract verification

### 2.3 End-to-End Tests

Full pipeline tests using recorded HTTP fixtures. These verify the complete workflow but are expensive to run and should be gated behind a test category/trait.

**Trait:** `[Trait("Category", "E2E")]`

---

## 3. Test Scenarios

### 3.1 Code Transformation

#### Happy Path

| ID | Scenario | Input | Expected Output |
|----|----------|-------|-----------------|
| CT-01 | Transform single C# file | `Program.cs` with .NET 8 patterns | Updated code with .NET 9 patterns; `Success = true` |
| CT-02 | Transform project file (.csproj) | `<TargetFramework>net8.0</TargetFramework>` | `<TargetFramework>net9.0</TargetFramework>` |
| CT-03 | Transform shell script | `dotnet-sdk-8.0` references | Updated SDK version references |
| CT-04 | Transform PowerShell script | .NET 8 SDK install commands | Updated to .NET 9 SDK commands |
| CT-05 | Batch transform multiple files | 5 C# files in same module | All transformed; token usage aggregated |
| CT-06 | Skill routing by content type | `CodeSample`, `ProjectFile`, `Configuration` | Correct skill file resolved per `SkillResolver` |
| CT-07 | Placeholder hydration | Template with `{{technology}}`, `{{fromVersion}}`, `{{toVersion}}` | All placeholders replaced with context values |

#### Edge Cases

| ID | Scenario | Input | Expected Output |
|----|----------|-------|-----------------|
| CT-E01 | Empty file content | `""` passed to `TransformContentAsync` | Graceful return; `Success = true`, content unchanged |
| CT-E02 | Very large file (>100KB) | Large C# file | Transformation completes or returns size-limit error |
| CT-E03 | Binary content accidentally classified | Non-text bytes | Graceful failure; `Success = false`; original content preserved |
| CT-E04 | File with no version references | C# file with no framework-specific code | Content returned unchanged; no spurious edits |
| CT-E05 | Already-upgraded file | File already at target version | Content returned unchanged; idempotent |
| CT-E06 | Mixed-version file | File referencing both .NET 7 and .NET 8 | Only source version upgraded; other versions untouched |
| CT-E07 | Unsupported file extension | `.ipynb`, `.png`, `.dll` | `SkillResolver` throws `ArgumentOutOfRangeException` for `Asset` type |

### 3.2 Docs Transformation

#### Happy Path

| ID | Scenario | Input | Expected Output |
|----|----------|-------|-----------------|
| DT-01 | Transform Markdown with code fences | ` ```csharp ` blocks referencing .NET 8 | Code fences updated; surrounding prose updated |
| DT-02 | Transform Markdown with version references | "Install .NET 8 SDK" in prose | Updated to ".NET 9 SDK" |
| DT-03 | Transform JSON config | `devcontainer.json` with SDK version | Version field updated |
| DT-04 | Transform YAML config | `.workshop.yml` with `version: "8.0"` | Version updated to `"9.0"` |
| DT-05 | Preserve non-upgrade content | Conceptual explanations with no version refs | Content unchanged |

#### Edge Cases

| ID | Scenario | Input | Expected Output |
|----|----------|-------|-----------------|
| DT-E01 | Markdown with broken fences | Unclosed code block | Best-effort transform; no crash |
| DT-E02 | Markdown referencing multiple technologies | `.NET 8` and `Node 20` in same file | Only target technology upgraded |
| DT-E03 | Empty Markdown file | `""` | Returned unchanged |
| DT-E04 | Markdown with HTML embedded | `<div>` blocks with version refs | HTML portions handled gracefully |
| DT-E05 | Config file with comments | YAML with inline comments | Comments preserved through transformation |

### 3.3 PR Generation

#### Happy Path

| ID | Scenario | Input | Expected Output |
|----|----------|-------|-----------------|
| PR-01 | Branch creation | Issue #42, target .NET 9 | Branch `workshop-upgrade/42-dotnet-9` created from `main` |
| PR-02 | Multi-commit strategy | 3 code files, 2 docs, 1 project file | 4 logical commits: project → code → docs → config |
| PR-03 | PR body structure | Completed transformation results | Markdown body with summary, changes-by-category table, validation status |
| PR-04 | PR labels applied | .NET upgrade | Labels: `workshop-upgrade`, `automated`, `dotnet` |
| PR-05 | PR references source issue | Issue #42 | Body contains `Closes #42` |
| PR-06 | PR mentions requestor | `@csharpfritz` filed issue | Body contains `@csharpfritz` mention |
| PR-07 | Change summary counts | 12 code, 8 docs, 3 project files | Summary shows correct file counts per category |

#### Edge Cases

| ID | Scenario | Input | Expected Output |
|----|----------|-------|-----------------|
| PR-E01 | Empty transformation (nothing changed) | All files already at target version | No PR created; comment on issue explaining "nothing to update" |
| PR-E02 | Partial transformation failure | 3/5 files transformed, 2 failed | PR created with successful changes; body lists failures with ⚠️ |
| PR-E03 | Branch already exists | Previous failed run left branch | Branch deleted and recreated, or incremental push |
| PR-E04 | Very long branch name | Long version string | Branch name truncated to Git's 255-char limit |
| PR-E05 | Default branch is not `main` | Repo uses `master` or `develop` | PR targets actual default branch |
| PR-E06 | Special characters in version | Target `9.0-preview.1` | Branch name sanitized: `workshop-upgrade/42-dotnet-9-0-preview-1` |
| PR-E07 | Single-file change | Only one `.csproj` updated | Single commit, not 4 empty commits |

---

## 4. Error & Failure Scenarios

### 4.1 Copilot API Errors

| ID | Scenario | Simulated Error | Expected Behavior |
|----|----------|-----------------|-------------------|
| ERR-01 | API returns 401 Unauthorized | `HttpRequestException` with 401 | `CopilotResponse.Success = false`; error logged; original content preserved |
| ERR-02 | API returns 429 Rate Limited | `HttpRequestException` with 429 + `Retry-After` | Retry with backoff up to configured limit; then fail gracefully |
| ERR-03 | API returns 500 Server Error | `HttpRequestException` with 500 | Retry transient failure; fail after max retries; preserve original |
| ERR-04 | API timeout | `TaskCanceledException` | `CopilotResponse.Success = false`; timeout message; content preserved |
| ERR-05 | API returns empty response | `Choices` array empty | `InvalidOperationException` caught; `Success = false` |
| ERR-06 | API returns malformed JSON | Invalid response body | Deserialization error caught; `Success = false` |
| ERR-07 | Network unreachable | `HttpRequestException` (no response) | `Success = false`; logged; original content preserved |
| ERR-08 | Skill file not found | Missing `.md` skill template | `FileNotFoundException` caught; `Success = false` |
| ERR-09 | API key expired mid-batch | 401 after several successful calls | Partial results preserved; remaining items fail; PR includes partial changes with warnings |

### 4.2 GitHub API Errors

| ID | Scenario | Simulated Error | Expected Behavior |
|----|----------|-----------------|-------------------|
| GH-01 | Branch creation fails (permission denied) | 403 from GitHub API | Comment on issue: "Insufficient permissions to create branch" |
| GH-02 | File commit fails | 409 conflict or 422 | Retry with fresh SHA; fail after max retries |
| GH-03 | PR creation fails | 422 Validation Failed | Comment on issue with error details |
| GH-04 | Label doesn't exist on repo | 404 when applying label | Skip label; don't fail the PR |
| GH-05 | Repo is archived | 403 from push | Comment on issue: "Repository is archived" |

### 4.3 Rate Limiting

| ID | Scenario | Setup | Expected Behavior |
|----|----------|-------|-------------------|
| RL-01 | Copilot API rate limit hit mid-batch | Fake returns 429 after N calls | Backoff and retry; resume batch from where it stopped |
| RL-02 | GitHub API rate limit | 403 with `X-RateLimit-Remaining: 0` | Wait until reset; retry |
| RL-03 | Token budget exceeded | Total tokens across batch > budget | Stop processing remaining files; create PR with completed work |

---

## 5. Test Fixture Requirements

### 5.1 Sample Workshop Repository

We need a **realistic multi-module workshop** fixture, not a toy example. This mirrors real workshop content Jeffrey maintains.

```
fixtures/sample-workshop/
├── .workshop.yml                    # Manifest declaring 3 modules
├── README.md                        # Workshop overview referencing .NET 8
├── module-01-setup/
│   ├── src/
│   │   ├── GettingStarted.csproj    # TargetFramework=net8.0
│   │   └── Program.cs              # WebApplication.CreateBuilder pattern
│   └── instructions.md             # "Install .NET 8 SDK" prose
├── module-02-routing/
│   ├── src/
│   │   ├── Routing.csproj          # TargetFramework=net8.0, package refs
│   │   └── Startup.cs              # .NET 8 routing patterns
│   └── instructions.md             # API routing instructions
├── module-03-data/
│   ├── src/
│   │   ├── DataAccess.csproj       # EF Core 8.x packages
│   │   └── AppDbContext.cs         # EF Core 8 patterns
│   └── instructions.md             # Database instructions
├── shared/
│   └── appsettings.json            # .NET 8 config patterns
└── .devcontainer/
    └── devcontainer.json           # SDK version reference
```

### 5.2 Specific Fixture Files Needed

| Fixture | Purpose | Format |
|---------|---------|--------|
| `csproj-net8.xml` | Project file with TargetFramework=net8.0 + PackageReferences | XML |
| `csproj-net9-expected.xml` | Expected output after transformation | XML |
| `program-cs-net8.cs.txt` | Minimal API Program.cs with .NET 8 patterns | C# text |
| `program-cs-net9-expected.cs.txt` | Expected transformation result | C# text |
| `instructions-net8.md` | Workshop instructions referencing .NET 8 | Markdown |
| `instructions-net9-expected.md` | Expected docs transformation result | Markdown |
| `devcontainer-net8.json` | Dev container with .NET 8 SDK | JSON |
| `pr-body-full.md` | Expected PR body for full upgrade | Markdown |
| `pr-body-partial-failure.md` | Expected PR body when some transforms fail | Markdown |
| `copilot-response-success.json` | Mock Copilot API success response | JSON |
| `copilot-response-error.json` | Mock Copilot API error response | JSON |
| `copilot-response-empty.json` | Mock Copilot API empty choices | JSON |
| `copilot-response-rate-limited.json` | Mock 429 response with Retry-After | JSON |
| `workshop-manifest-3module.yml` | 3-module workshop manifest | YAML |
| `empty-repo-tree.json` | Repository with no recognizable content | JSON |

### 5.3 Fixture Conventions

Follow existing patterns established in Phase 1-2:

- Store in `tests/WorkshopManager.UnitTests/Fixtures/`
- Load via `TestFixtureLoader` (file-system based, `CopyToOutputDirectory: PreserveNewest`)
- Markdown fixtures: title on line 1, body after blank line
- Use `.txt` extension for code fixtures to avoid IDE/build confusion (e.g., `Program.cs.txt`)
- Expected output fixtures use `-expected` suffix

---

## 6. Mocking Strategy

### 6.1 Copilot API Mocking

**Unit tests: NSubstitute mocks of `ICopilotClient`**

```csharp
// Pattern: substitute returns deterministic responses
var copilotClient = Substitute.For<ICopilotClient>();
copilotClient.TransformContentAsync(
    Arg.Any<string>(),
    Arg.Any<string>(),
    Arg.Any<CopilotContext>(),
    Arg.Any<CancellationToken>())
    .Returns(new CopilotResponse("transformed content", true, null, 150));
```

**Integration tests: `FakeCopilotClient` with deterministic behavior**

Unlike `StubCopilotClient` (which returns content unchanged), `FakeCopilotClient` will:

1. Actually transform content in a predictable way (e.g., string-replace version references)
2. Track call counts for batching assertions
3. Support configurable failure injection (fail on Nth call, specific file paths, etc.)
4. Record all `CopilotContext` values passed in for assertion

```csharp
// Conceptual shape — NOT final implementation
public class FakeCopilotClient : ICopilotClient
{
    public List<CopilotContext> RecordedCalls { get; } = [];
    public int FailAfterNCalls { get; set; } = int.MaxValue;
    public Dictionary<string, string> FileSpecificResponses { get; } = new();
    
    // Performs simple version string replacement instead of AI transformation
}
```

### 6.2 GitHub API Mocking

**Unit tests:** NSubstitute mocks of `Octokit.IGitHubClient` (or whatever wrapper we define).

**Integration tests:** Recorded HTTP responses via `HttpMessageHandler` fake:

- Branch creation: 201 Created / 422 Already Exists / 403 Forbidden
- File commit: 201 Created / 409 Conflict
- PR creation: 201 Created / 422 Validation Failed
- Label application: 200 OK / 404 Not Found

### 6.3 Content Provider Mocking

Continue using `InMemoryContentProvider` (already exists) for unit tests. For integration tests, create fixtures that represent full repository trees loaded from the sample workshop fixture directory.

### 6.4 What We Do NOT Mock

- `SkillResolver` — test the real routing logic; it's pure and fast
- `FileClassifier` — already tested with 43 tests; use real implementation
- PR body builder — test actual Markdown generation output
- Branch name generator — test actual slug logic

---

## 7. Test Infrastructure Additions

### 7.1 New Test Helpers Needed

| Helper | Purpose |
|--------|---------|
| `FakeCopilotClient` | Deterministic Copilot API replacement with call recording and failure injection |
| `FakeGitHubApiClient` | Records branch/commit/PR API calls; configurable failures |
| `TransformationResultBuilder` | Fluent builder for constructing multi-file transformation results |
| `PrBodyAssertions` | FluentAssertions extensions: `.HaveChangeCount()`, `.ContainIssueReference()`, `.ListFailedFiles()` |
| `WorkshopFixtureLoader` | Loads the multi-module sample workshop into `InMemoryContentProvider` |

### 7.2 Test Traits

| Trait | Purpose |
|-------|---------|
| `[Trait("Category", "Unit")]` | Fast, isolated, no I/O |
| `[Trait("Category", "Integration")]` | WebApplicationFactory, DI overrides |
| `[Trait("Category", "E2E")]` | Full pipeline with HTTP fixtures |
| `[Trait("Phase", "3")]` | All Phase 3 tests (for CI filtering) |

---

## 8. Estimated Test Counts

| Category | Area | Estimated Tests |
|----------|------|-----------------|
| Unit | Code Transformation | 15-20 |
| Unit | Docs Transformation | 10-15 |
| Unit | SkillResolver routing | 5-8 |
| Unit | PR body builder | 10-15 |
| Unit | Branch name generator | 8-10 |
| Unit | Transformation orchestrator | 10-15 |
| Integration | Full pipeline (webhook → PR) | 5-8 |
| Integration | Partial failure flows | 5-8 |
| Integration | Error handling flows | 8-12 |
| E2E | Complete upgrade scenarios | 3-5 |
| **Total** | | **~80-115** |

---

## 9. Open Questions for Architecture

These affect test design and are pending finalization:

1. **Transformation orchestrator interface** — Is there a single `ITransformationOrchestrator` that takes `WorkshopStructure` + `UpgradeIntent` and returns all changes? Or do we call `ICopilotClient` per-file from the webhook handler? This determines where batching/ordering tests live.

2. **PR creation abstraction** — Is there an `IPullRequestService` wrapping Octokit, or do we call Octokit directly? Wrapper is easier to mock and test.

3. **Retry policy location** — Does retry/backoff live in `CopilotClient` itself, or in a Polly policy on the `HttpClient`? Affects how we simulate rate limiting in tests.

4. **Token budget tracking** — Is there a `ITokenBudget` service that tracks cumulative usage, or is it per-call only? Affects RL-03 test design.

5. **Partial PR behavior** — When some files fail transformation, does the PR include only successes? Or does it block entirely? Determines PR-E02 expected behavior.

---

## 10. Dependencies & Prerequisites

- [ ] Architecture design for transformation orchestrator finalized
- [ ] `IPullRequestService` (or equivalent) interface defined
- [ ] Skill `.md` template files available in `Skills/` directory
- [ ] Decision on retry/backoff strategy (Polly vs custom)
- [ ] Sample workshop fixture content reviewed by Jeffrey for realism
