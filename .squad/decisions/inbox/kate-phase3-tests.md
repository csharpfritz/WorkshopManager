# Decision: Phase 3 Test Strategy

> **From:** Kate (Tester)  
> **Date:** 2026-02-22  
> **Status:** Proposed  
> **Affects:** Phase 3 — Transformation & PR Generation

---

## Decisions Proposed

### 1. FakeCopilotClient for Integration Tests (not StubCopilotClient)

**What:** Integration tests should use a new `FakeCopilotClient` that performs deterministic string-replacement transformations, records calls, and supports failure injection — rather than the existing `StubCopilotClient` which returns content unchanged.

**Why:** `StubCopilotClient` is a pass-through. It can't verify that the orchestrator actually sends the right content, in the right order, with the right context. A `FakeCopilotClient` that does predictable version-string replacement lets us assert on actual transformed output in integration tests without depending on the real Copilot API.

**Impact:** New test helper class. Does not affect production code.

### 2. Partial PR on Partial Failure — Need Team Decision

**What:** When some files fail Copilot transformation (e.g., API error on 2 of 5 files), should we:
- (a) Create a PR with only the successful transformations + warning list, or
- (b) Block the entire PR and comment on the issue?

**My recommendation:** Option (a) — partial PR with warnings. Authors can review what worked and manually handle the rest. A blocked PR gives them nothing.

**Needs input from:** Kamala (architecture), Jeffrey (product)

### 3. Test Fixture: Realistic Multi-Module Workshop

**What:** Phase 3 tests need a sample workshop fixture with 3 modules, `.csproj` files, Markdown instructions, config files, and a `.workshop.yml` manifest — representing a real .NET 8 → .NET 9 upgrade.

**Why:** Toy examples (single file) miss real-world complexity: module grouping, cross-file version consistency, mixed content types. The fixture should be reviewed by Jeffrey to ensure it mirrors his actual workshops.

**Impact:** New fixtures in `tests/WorkshopManager.UnitTests/Fixtures/`. Reusable across unit and integration tests.

### 4. Use `.txt` Extension for Code Fixtures

**What:** Code sample fixtures (e.g., `Program.cs` content) should use `.cs.txt` extension, not `.cs`, to prevent the IDE/build from treating them as compilable source.

**Why:** Existing pattern — Phase 2 fixtures already use `sample.csproj.txt` and `sample-package.json.txt`. Consistency prevents accidental compilation.

### 5. Trait-Based Test Filtering for E2E Tests

**What:** End-to-end tests (full webhook → PR pipeline with HTTP fixtures) should be tagged `[Trait("Category", "E2E")]` and excluded from the default `dotnet test` run.

**Why:** E2E tests are slow and brittle. CI should run unit + integration by default, with E2E as a separate pipeline stage. This matches the existing dual-project pattern (unit tests fast, integration tests boot full app).

---

## Open Questions for Team

1. Is there a single `ITransformationOrchestrator` interface planned, or does the webhook handler call `ICopilotClient` per-file? Test structure depends on this.
2. Will retry/backoff for Copilot API use Polly policies on `HttpClient`, or custom logic in `CopilotClient`? Determines mocking approach for rate limit tests.
3. Is there a token budget tracker service planned? Affects how we test "stop after exceeding budget" behavior.
4. Should `IPullRequestService` be a new abstraction wrapping Octokit, or do we test against Octokit interfaces directly?
