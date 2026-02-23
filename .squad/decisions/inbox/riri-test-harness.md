# Decision: FakeCopilotClient Integration Test Pattern

**By:** Riri
**Date:** 2026-02-23
**Context:** Building UpgradeOrchestrator walkthrough integration tests

## Decision

Created `FakeCopilotClient` in the integration test project (`Helpers/FakeCopilotClient.cs`) rather than modifying the existing `StubCopilotClient` in the API project.

**Why not modify StubCopilotClient?**
- StubCopilotClient is a production stub (Phase 1 pass-through) — it has a clear purpose and shouldn't carry test-only configuration baggage
- FakeCopilotClient needs test-specific features: configurable response handler via `Func<string, CopilotContext, CopilotResponse>`, call tracking via `.Calls` list
- Keeps test infrastructure in the test project where it belongs

**Pattern established:**
- `FakeCopilotClient.OnTransform(handler)` — set per-file response logic
- `FakePullRequestService` — avoids hitting GitHub API, records calls for assertions
- DI override via `WebApplicationFactory.WithWebHostBuilder` + `ConfigureServices` — register fakes as singletons to shadow scoped production registrations

## Impact
- Kate can reuse FakeCopilotClient for additional integration test scenarios
- Pattern is consistent with Kate's Phase 3 test strategy (FakeCopilotClient for deterministic transforms)
- No changes to production code required
