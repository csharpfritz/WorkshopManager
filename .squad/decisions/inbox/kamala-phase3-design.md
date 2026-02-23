# Decision: Phase 3 Transformation & PR Architecture (WI-14)

**Author:** Kamala (Lead)  
**Date:** 2026-02-22  
**Status:** Proposed  
**Design Document:** `docs/design/phase3-transformation-pr.md`

---

## Decisions

### D1: Separate Code and Documentation Transformation Services
**What:** Two interfaces — `ICodeTransformationService` (code, project files, config) and `IDocumentationTransformationService` (markdown/prose). Not one unified service.  
**Why:** Different Copilot prompt strategies, different context window needs (docs may include related code snippets), different failure severity. Separate services = separate mocks for testing.

### D2: TransformationResult Per File
**What:** Each file produces an individual `TransformationResult`. `TransformationSummary` aggregates all results.  
**Why:** Per-file granularity enables partial success reporting in the PR description, per-file retry (future), and accurate token accounting.

### D3: Sequential Copilot Calls
**What:** Files are transformed one at a time, not in parallel.  
**Why:** Copilot rate limits are not documented. Sequential is safe and debuggable. Controlled parallelism (e.g., `SemaphoreSlim(3)`) can be introduced after observing production behavior.

### D4: Git Data API for Multi-Commit Branches
**What:** Use Git Data API (blobs → trees → commits → refs) instead of push-based flow.  
**Why:** No local clone needed (remote-first principle from Phase 2). Precise control over multi-commit structure. Azure Functions compatible.

### D5: Branch Recreate on Retry
**What:** If `workshop-upgrade/{issue}-{version}` branch exists, delete and recreate from current default branch HEAD.  
**Why:** Ensures clean state. Avoids merge commits on automation-owned branches.

### D6: Failed Files Excluded from PR
**What:** Files that fail Copilot transformation are NOT committed. They appear in the PR description failure table.  
**Why:** Committing unchanged originals misleads reviewers. Committing broken output is worse. Clean separation: PR = validated changes only.

### D7: UpgradeOrchestrator as Pipeline Owner
**What:** Single `IUpgradeOrchestrator` service orchestrates intent → analysis → transformation → PR.  
**Why:** Webhook handler stays thin. Orchestrator is testable, composable, single entry point.

---

## Impact

| Agent | Impact |
|-------|--------|
| **Riri** | Implements `ICodeTransformationService` (WI-15) and `IDocumentationTransformationService` (WI-16) per interfaces defined in design doc. |
| **America** | Implements `IPullRequestService` (WI-17) using Git Data API. Branch naming, commit strategy, PR template all specified. |
| **Kate** | Can mock all four service interfaces for E2E tests (WI-19). `UpgradeOrchestrator` is the test entry point. |
| **Kamala** | Reviews implementations against this design in WI-22. |

## New Models
- `TransformationResult` — per-file outcome
- `TransformationSummary` — aggregate with computed Succeeded/Failed/Unchanged
- `PullRequestResult` — PR creation outcome
- `UpgradeResult` + `UpgradePhase` — top-level orchestrator result

## New Interfaces
- `ICodeTransformationService`
- `IDocumentationTransformationService`
- `IPullRequestService`
- `IUpgradeOrchestrator`

## DI Registration
All Phase 3 services: `Scoped` lifetime (matches `GitHubContentProvider` token caching).
