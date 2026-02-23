# Decision: WI-17 PR Generation Service Implementation

**Author:** America (Frontend Dev)  
**Date:** 2026-02-22  
**Status:** Implemented

## What Was Built

IPullRequestService interface and PullRequestService implementation for creating multi-commit PRs from transformation results via the GitHub Git Data API.

## Key Decisions

1. **Auth pattern reused from GitHubContentProvider** — JWT generation and installation token caching follow the exact same pattern. If we refactor auth into a shared service later, both classes benefit.

2. **Branch naming includes technology slug** — Per design §6.1: `workshop-upgrade/{issue}-{tech}-{version}`. This prevents collisions across forks or multiple upgrade types.

3. **Idempotency via delete-and-recreate** — Per design D5: if branch exists, delete it and recreate from current default branch HEAD. Existing PR gets its title/body updated rather than creating a duplicate.

4. **Labels are auto-created if missing** — The service creates `workshop-upgrade`, `automated`, and technology labels with a default color if they don't exist. Failures to create labels are logged as warnings but don't fail the PR.

5. **Placeholder models created** — TransformationResult, TransformationSummary, PullRequestResult, and UpgradeResult were created matching the design doc exactly. Riri is building these in parallel; if her versions land first, reconcile by keeping whichever matches the design spec.

## Files Created

- `src/WorkshopManager.Api/Models/TransformationResult.cs`
- `src/WorkshopManager.Api/Models/TransformationSummary.cs`
- `src/WorkshopManager.Api/Models/PullRequestResult.cs`
- `src/WorkshopManager.Api/Models/UpgradeResult.cs`
- `src/WorkshopManager.Api/Services/IPullRequestService.cs`
- `src/WorkshopManager.Api/Services/PullRequestService.cs`

## Files Modified

- `src/WorkshopManager.Api/Program.cs` — Added Scoped DI registration for IPullRequestService

## Coordination Notes

- Models match design doc §2 verbatim. If Riri's versions differ, the design doc is the source of truth.
- BuildBranchName and BuildPrBody are `internal static` for testability — Kate can test them directly.
