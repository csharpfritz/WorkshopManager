# Decision: Phase 3 Transformation Service Implementation
**By:** Riri  
**Date:** 2026-02-22  
**Work Items:** WI-15, WI-16

## Decisions Made

### D1: Orchestrator uses IRepositoryContentProvider instead of IGitHubClientService
**What:** The design §5.1 pseudocode references `IGitHubClientService.GetDefaultBranchHeadAsync` which doesn't exist. Instead of creating a new interface, the orchestrator uses `IRepositoryContentProvider.GetFileTreeAsync(repo, "HEAD")` to validate repo access and passes "HEAD" as the commit SHA.
**Why:** IRepositoryContentProvider already talks to GitHub and resolves refs. Adding a separate IGitHubClientService for a single method would mean a new interface, new implementation, and new DI registration — all for something the content provider already handles. When a real commit SHA resolution is needed (e.g., for idempotent retry), IGitHubClientService can be introduced then.
**Impact:** UpgradeOrchestrator has one fewer dependency. No new unimplemented interfaces.

### D2: IPullRequestService not registered in DI
**What:** The IPullRequestService interface exists but has no implementation yet (America is building it in WI-17). It is NOT registered in DI.
**Why:** Registering without an implementation would fail at resolve time regardless. The UpgradeOrchestrator compiles and is registered, but cannot be resolved until IPullRequestService has an implementation registered.
**Impact:** America must register `IPullRequestService → PullRequestService` in Program.cs when WI-17 ships.

### D3: upgrade-configuration.md created as separate skill
**What:** Created a new `upgrade-configuration.md` skill template alongside the existing `upgrade-project-file.md`.
**Why:** Configuration files (Dockerfiles, devcontainer.json, CI/CD workflows, appsettings) have different concerns than project files (.csproj, package.json). A dedicated skill lets Copilot focus on config-specific patterns. The SkillResolver still routes Configuration → upgrade-project-file.md per design §8.3. The new skill is available for future routing refinement.
**Impact:** No runtime behavior change. The skill file is deployed to output directory via existing `Skills\*.md` wildcard in .csproj.

## For Kamala to Review
- Should we update SkillResolver to route `ContentItemType.Configuration` → `upgrade-configuration.md` instead of `upgrade-project-file.md`?
- The orchestrator's "HEAD" commit SHA approach works but loses the idempotency guarantee from §6.4. When should we add proper SHA resolution?
