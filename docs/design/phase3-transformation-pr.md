# Phase 3: Transformation & PR — Design Document

> **WI-14** | **Author:** Kamala (Lead) | **Date:** 2026-02-22  
> **Status:** Proposed  
> **Implements:** PRD Sections 5, 6, 7 — Copilot Integration, Content Analysis, PR Generation

---

## 1. Overview

Phase 3 takes the `WorkshopStructure` produced by Phase 2 and the `UpgradeIntent` from Phase 1, transforms every relevant file through Copilot, and delivers a multi-commit PR via Octokit. This document defines the service interfaces, data flow, orchestration, error handling, and DI registration for the entire transformation-through-PR pipeline.

### Design Principles

1. **Per-file fault isolation** — A single file failure must not abort the upgrade. Track partial success explicitly.
2. **Separate code and prose transformation** — Different Copilot prompt strategies; separate services for testability.
3. **Multi-commit PRs** — Logical commits by category (project files → code → docs → config) per PRD §7.
4. **Remote-first** — All operations via GitHub API. No local clone.
5. **Idempotent retry** — Re-running transformation for the same issue+commit produces the same branch/PR (upsert semantics).

---

## 2. Data Model

### 2.1 TransformationResult

Captures the outcome of transforming a single file.

```csharp
namespace WorkshopManager.Models;

/// <summary>
/// Result of transforming a single content item through Copilot.
/// </summary>
public record TransformationResult
{
    /// <summary>Path relative to repo root.</summary>
    public required string Path { get; init; }

    /// <summary>Content type that determined the transformation strategy.</summary>
    public required ContentItemType ContentType { get; init; }

    /// <summary>Whether the transformation succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Transformed content. Null on failure; original content preserved.</summary>
    public string? TransformedContent { get; init; }

    /// <summary>Original content before transformation.</summary>
    public required string OriginalContent { get; init; }

    /// <summary>Error message if transformation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Copilot tokens consumed for this file.</summary>
    public int TokensUsed { get; init; }

    /// <summary>Whether the content actually changed (success + diff detected).</summary>
    public bool HasChanges => Success
        && TransformedContent is not null
        && TransformedContent != OriginalContent;
}
```

### 2.2 TransformationSummary

Aggregates results across all files in an upgrade job.

```csharp
namespace WorkshopManager.Models;

/// <summary>
/// Aggregated results from transforming all content items in a workshop upgrade.
/// </summary>
public record TransformationSummary
{
    /// <summary>All per-file results, including successes and failures.</summary>
    public required IReadOnlyList<TransformationResult> Results { get; init; }

    /// <summary>The upgrade intent that drove this transformation.</summary>
    public required UpgradeIntent Intent { get; init; }

    /// <summary>The workshop structure that was transformed.</summary>
    public required WorkshopStructure Structure { get; init; }

    // Computed properties
    public IReadOnlyList<TransformationResult> Succeeded =>
        Results.Where(r => r.HasChanges).ToList();

    public IReadOnlyList<TransformationResult> Failed =>
        Results.Where(r => !r.Success).ToList();

    public IReadOnlyList<TransformationResult> Unchanged =>
        Results.Where(r => r.Success && !r.HasChanges).ToList();

    public int TotalTokensUsed => Results.Sum(r => r.TokensUsed);

    public bool HasAnyChanges => Succeeded.Count > 0;
}
```

### 2.3 PullRequestResult

Captures the outcome of PR generation.

```csharp
namespace WorkshopManager.Models;

/// <summary>
/// Result of creating a pull request from transformation results.
/// </summary>
public record PullRequestResult
{
    /// <summary>Whether the PR was created successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>PR number, if created.</summary>
    public int? PullRequestNumber { get; init; }

    /// <summary>PR URL, if created.</summary>
    public string? PullRequestUrl { get; init; }

    /// <summary>Branch name used for the PR.</summary>
    public string? BranchName { get; init; }

    /// <summary>Number of commits created.</summary>
    public int CommitCount { get; init; }

    /// <summary>Error message if PR creation failed.</summary>
    public string? ErrorMessage { get; init; }
}
```

---

## 3. Service Interfaces

### 3.1 ICodeTransformationService

Handles transformation of code files (`CodeSample`, `ProjectFile`, `Configuration`).

```csharp
namespace WorkshopManager.Services;

/// <summary>
/// Transforms code files (source code, project files, config) using Copilot.
/// Processes files individually with per-file error isolation.
/// </summary>
public interface ICodeTransformationService
{
    /// <summary>
    /// Transform a batch of code-related content items.
    /// </summary>
    /// <param name="items">Content items to transform (CodeSample, ProjectFile, Configuration).</param>
    /// <param name="intent">The upgrade intent driving transformation.</param>
    /// <param name="structure">Workshop structure for context.</param>
    /// <param name="repoFullName">Repository in "owner/repo" format.</param>
    /// <param name="commitSha">Commit SHA for consistent file reads.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Per-file transformation results.</returns>
    Task<IReadOnlyList<TransformationResult>> TransformAsync(
        IReadOnlyList<ContentItem> items,
        UpgradeIntent intent,
        WorkshopStructure structure,
        string repoFullName,
        string commitSha,
        CancellationToken ct = default);
}
```

**Design notes:**

- Accepts a batch but processes each file individually against Copilot. This allows per-file error isolation while keeping the interface clean.
- The implementation fetches each file's content via `IRepositoryContentProvider`, resolves the skill prompt via `ISkillResolver`, and calls `ICopilotClient.TransformContentAsync`.
- Files are processed sequentially (not parallel) to respect Copilot rate limits. Parallelism is a future optimization gated on rate limit behavior.
- `ProjectFile` and `Configuration` items use distinct skill prompts from `CodeSample` — the `ISkillResolver` handles routing.

### 3.2 IDocumentationTransformationService

Handles transformation of prose/markdown files (`Documentation`).

```csharp
namespace WorkshopManager.Services;

/// <summary>
/// Transforms documentation files (markdown, prose) using Copilot.
/// Uses prose-specific prompting strategy distinct from code transformation.
/// </summary>
public interface IDocumentationTransformationService
{
    /// <summary>
    /// Transform a batch of documentation content items.
    /// </summary>
    /// <param name="items">Content items to transform (Documentation only).</param>
    /// <param name="intent">The upgrade intent driving transformation.</param>
    /// <param name="structure">Workshop structure for context.</param>
    /// <param name="repoFullName">Repository in "owner/repo" format.</param>
    /// <param name="commitSha">Commit SHA for consistent file reads.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Per-file transformation results.</returns>
    Task<IReadOnlyList<TransformationResult>> TransformAsync(
        IReadOnlyList<ContentItem> items,
        UpgradeIntent intent,
        WorkshopStructure structure,
        string repoFullName,
        string commitSha,
        CancellationToken ct = default);
}
```

**Why separate from code transformation?**

1. **Different prompt strategy** — Prose transformation must preserve teaching flow, narrative structure, and pedagogical intent. The `upgrade-documentation.md` skill emphasizes different concerns than `upgrade-code-sample.md`.
2. **Different context window needs** — Documentation files may reference multiple code files. The documentation transformer can optionally include related code snippets in the prompt to keep prose and code in sync.
3. **Different failure semantics** — A documentation failure is lower severity than a code failure. The PR description should communicate this distinction.
4. **Testability** — Separate services mean separate mocks. Kate can test code transformation independently from documentation transformation.

### 3.3 IPullRequestService

Creates branches, commits transformed files, and opens PRs via Octokit.

```csharp
namespace WorkshopManager.Services;

/// <summary>
/// Creates branches, commits transformed files in logical groups, and opens
/// pull requests via the GitHub API.
/// </summary>
public interface IPullRequestService
{
    /// <summary>
    /// Generate a PR from transformation results.
    /// Creates a branch, makes logical commits by content category, and opens a PR.
    /// </summary>
    /// <param name="summary">Transformation results to commit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>PR creation result.</returns>
    Task<PullRequestResult> CreatePullRequestAsync(
        TransformationSummary summary,
        CancellationToken ct = default);
}
```

**Implementation responsibilities:**

1. **Branch creation** — `workshop-upgrade/{issue-number}-{target-version}` from default branch HEAD.
2. **Logical commits** — Group `TransformationResult` by `ContentType`, create one commit per category:
   - `chore: update project files to {target-version}` — `ProjectFile` items
   - `refactor: update code samples for {target-version}` — `CodeSample` items
   - `docs: update instructions for {target-version}` — `Documentation` items
   - `chore: update configuration files` — `Configuration` items
3. **PR body generation** — Build structured markdown summarizing changes, failures, and review guidance.
4. **PR metadata** — Labels (`workshop-upgrade`, `automated`, technology tag), linked issue (`Closes #{issueNumber}`), assignee (requestor).
5. **Idempotency** — If branch already exists, force-update it. If PR already exists for that branch, update the existing PR.

### 3.4 IUpgradeOrchestrator

Top-level pipeline that ties all phases together.

```csharp
namespace WorkshopManager.Services;

/// <summary>
/// Orchestrates the complete workshop upgrade pipeline:
/// issue parsing → content analysis → transformation → PR generation.
/// </summary>
public interface IUpgradeOrchestrator
{
    /// <summary>
    /// Execute the full upgrade workflow for a parsed upgrade intent.
    /// </summary>
    /// <param name="intent">Parsed upgrade intent from issue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The PR result, or failure information.</returns>
    Task<UpgradeResult> ExecuteAsync(
        UpgradeIntent intent,
        CancellationToken ct = default);
}
```

Supporting result type:

```csharp
namespace WorkshopManager.Models;

/// <summary>
/// Top-level result from the upgrade orchestrator.
/// </summary>
public record UpgradeResult
{
    public required bool Success { get; init; }

    /// <summary>PR details if created.</summary>
    public PullRequestResult? PullRequest { get; init; }

    /// <summary>Transformation details.</summary>
    public TransformationSummary? TransformationSummary { get; init; }

    /// <summary>Error message for top-level failures (e.g., analysis failure).</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Which phase failed, if any.</summary>
    public UpgradePhase? FailedPhase { get; init; }
}

public enum UpgradePhase
{
    Analysis,
    Transformation,
    PullRequestGeneration
}
```

---

## 4. Data Flow

### 4.1 End-to-End Pipeline

```
                         ┌─────────────────────────────────────────────────────────┐
                         │                  UpgradeOrchestrator                     │
                         │                                                         │
  UpgradeIntent ────────►│  1. Resolve commit SHA (default branch HEAD)            │
  (from webhook)         │     └─► IGitHubClientService.GetDefaultBranchHeadAsync  │
                         │                                                         │
                         │  2. Analyze workshop structure                           │
                         │     └─► IWorkshopAnalyzer.AnalyzeAsync                  │
                         │     └─► WorkshopStructure                               │
                         │                                                         │
                         │  3. Filter items by UpgradeScope                        │
                         │     └─► Full: all transformable types                   │
                         │     └─► CodeOnly: CodeSample + ProjectFile + Config     │
                         │     └─► DocsOnly: Documentation only                    │
                         │     └─► Incremental: all (with analysis skill)          │
                         │                                                         │
                         │  4. Transform code items                                │
                         │     └─► ICodeTransformationService.TransformAsync       │
                         │     └─► IReadOnlyList<TransformationResult>             │
                         │                                                         │
                         │  5. Transform documentation items                       │
                         │     └─► IDocumentationTransformationService             │
                         │     └─► IReadOnlyList<TransformationResult>             │
                         │                                                         │
                         │  6. Aggregate into TransformationSummary                │
                         │                                                         │
                         │  7. Generate PR (if any changes)                        │
                         │     └─► IPullRequestService.CreatePullRequestAsync      │
                         │     └─► PullRequestResult                               │
                         │                                                         │
                         │  8. Return UpgradeResult                                │
                         └─────────────────────────────────────────────────────────┘
```

### 4.2 Code Transformation Flow (per file)

```
ContentItem ──► IRepositoryContentProvider.GetFileContentAsync ──► original content
                                                                        │
                                                                        ▼
ISkillResolver.ResolveSkillPath(item.Type, intent.Scope) ──► skill prompt path
                                                                        │
                                                                        ▼
                                                              ┌────────────────┐
                                                              │ Build          │
                                                              │ CopilotContext │
                                                              │ (repo, path,   │
                                                              │  from, to,     │
                                                              │  technology)   │
                                                              └───────┬────────┘
                                                                      │
                                                                      ▼
ICopilotClient.TransformContentAsync(content, skillPath, context) ──► CopilotResponse
                                                                        │
                                                                        ▼
                                                              ┌────────────────┐
                                                              │ Build          │
                                                              │ Transformation │
                                                              │ Result         │
                                                              └────────────────┘
```

### 4.3 PR Generation Flow

```
TransformationSummary
    │
    ├── Group results by ContentType:
    │   ├── ProjectFile[]     ──► Commit 1: "chore: update project files to {version}"
    │   ├── CodeSample[]      ──► Commit 2: "refactor: update code samples for {version}"
    │   ├── Documentation[]   ──► Commit 3: "docs: update instructions for {version}"
    │   └── Configuration[]   ──► Commit 4: "chore: update configuration files"
    │
    ├── For each commit group:
    │   1. Create/update tree via Git Tree API
    │   2. Create commit pointing to new tree + parent
    │   3. Update branch ref
    │
    ├── Build PR description:
    │   ├── Summary stats (files changed, failed, tokens)
    │   ├── Changes by category (table per type)
    │   ├── Failures section (if any)
    │   ├── REVIEW markers inventory
    │   └── Footer: "Closes #{issueNumber}"
    │
    └── Create/update PR via Octokit
        ├── Labels: workshop-upgrade, automated, {technology}
        └── Assignee: intent.RequestorLogin
```

---

## 5. Orchestrator Implementation

### 5.1 Pseudocode

```csharp
public class UpgradeOrchestrator : IUpgradeOrchestrator
{
    private readonly IWorkshopAnalyzer _analyzer;
    private readonly ICodeTransformationService _codeTransformer;
    private readonly IDocumentationTransformationService _docsTransformer;
    private readonly IPullRequestService _prService;
    private readonly IGitHubClientService _github;
    private readonly ILogger<UpgradeOrchestrator> _logger;

    public async Task<UpgradeResult> ExecuteAsync(UpgradeIntent intent, CancellationToken ct)
    {
        // 1. Get consistent commit reference
        var commitSha = await _github.GetDefaultBranchHeadAsync(intent.RepoFullName, ct);

        // 2. Analyze workshop structure
        WorkshopStructure structure;
        try
        {
            structure = await _analyzer.AnalyzeAsync(intent.RepoFullName, commitSha, ct);
        }
        catch (Exception ex)
        {
            return new UpgradeResult
            {
                Success = false,
                ErrorMessage = $"Workshop analysis failed: {ex.Message}",
                FailedPhase = UpgradePhase.Analysis
            };
        }

        // 3. Partition items by transformation service
        var (codeItems, docItems) = PartitionItems(structure.Items, intent.Scope);

        // 4. Transform
        var results = new List<TransformationResult>();

        if (codeItems.Count > 0)
        {
            var codeResults = await _codeTransformer.TransformAsync(
                codeItems, intent, structure, intent.RepoFullName, commitSha, ct);
            results.AddRange(codeResults);
        }

        if (docItems.Count > 0)
        {
            var docResults = await _docsTransformer.TransformAsync(
                docItems, intent, structure, intent.RepoFullName, commitSha, ct);
            results.AddRange(docResults);
        }

        // 5. Build summary
        var summary = new TransformationSummary
        {
            Results = results,
            Intent = intent,
            Structure = structure
        };

        // 6. Bail if nothing changed (all failed or no diffs)
        if (!summary.HasAnyChanges)
        {
            return new UpgradeResult
            {
                Success = false,
                TransformationSummary = summary,
                ErrorMessage = summary.Failed.Count > 0
                    ? $"All {summary.Failed.Count} transformations failed."
                    : "No changes detected. Workshop may already be at target version.",
                FailedPhase = UpgradePhase.Transformation
            };
        }

        // 7. Generate PR
        try
        {
            var prResult = await _prService.CreatePullRequestAsync(summary, ct);
            return new UpgradeResult
            {
                Success = prResult.Success,
                PullRequest = prResult,
                TransformationSummary = summary,
                ErrorMessage = prResult.ErrorMessage,
                FailedPhase = prResult.Success ? null : UpgradePhase.PullRequestGeneration
            };
        }
        catch (Exception ex)
        {
            return new UpgradeResult
            {
                Success = false,
                TransformationSummary = summary,
                ErrorMessage = $"PR generation failed: {ex.Message}",
                FailedPhase = UpgradePhase.PullRequestGeneration
            };
        }
    }

    private static (IReadOnlyList<ContentItem> Code, IReadOnlyList<ContentItem> Docs)
        PartitionItems(IReadOnlyList<ContentItem> items, UpgradeScope scope)
    {
        var transformable = items.Where(i => i.Type != ContentItemType.Asset);

        var filtered = scope switch
        {
            UpgradeScope.CodeOnly => transformable.Where(i =>
                i.Type is ContentItemType.CodeSample
                    or ContentItemType.ProjectFile
                    or ContentItemType.Configuration),
            UpgradeScope.DocsOnly => transformable.Where(i =>
                i.Type is ContentItemType.Documentation),
            _ => transformable // Full and Incremental process everything
        };

        var code = filtered
            .Where(i => i.Type is not ContentItemType.Documentation)
            .ToList();

        var docs = filtered
            .Where(i => i.Type is ContentItemType.Documentation)
            .ToList();

        return (code, docs);
    }
}
```

---

## 6. PR Generation Details

### 6.1 Branch Naming

```
workshop-upgrade/{issue-number}-{technology}-{target-version}
```

Examples:
- `workshop-upgrade/42-dotnet-9.0`
- `workshop-upgrade/17-node-22`

The technology slug is included to prevent branch collisions if multiple upgrade issues target the same issue number across forks.

### 6.2 Commit Strategy

Commits are created via the GitHub Git Data API (not push-based), which avoids needing a local clone:

1. **Create blobs** — One blob per changed file via `POST /repos/{owner}/{repo}/git/blobs`.
2. **Create tree** — Tree for each commit group via `POST /repos/{owner}/{repo}/git/trees` with `base_tree` from parent.
3. **Create commit** — `POST /repos/{owner}/{repo}/git/commits` with tree SHA and parent SHA.
4. **Update reference** — `PATCH /repos/{owner}/{repo}/git/refs/heads/{branch}` to advance the branch.

Commit order (matching PRD §7):

| Order | Commit Message | Content Types |
|-------|---------------|---------------|
| 1 | `chore: update project files to {target-version}` | `ProjectFile` |
| 2 | `refactor: update code samples for {target-version}` | `CodeSample` |
| 3 | `docs: update instructions for {target-version}` | `Documentation` |
| 4 | `chore: update configuration files` | `Configuration` |

Empty groups are skipped (no empty commits).

### 6.3 PR Description Template

```markdown
## 🔄 Workshop Upgrade: {technology} {source-version} → {target-version}

This PR upgrades the workshop content from {technology} {source-version} to {target-version}.

Requested by @{requestor} in #{issue-number}.

### Summary

| Category | Files Changed | Files Failed | Files Unchanged |
|----------|:------------:|:------------:|:---------------:|
| Project Files | {n} | {n} | {n} |
| Code Samples | {n} | {n} | {n} |
| Documentation | {n} | {n} | {n} |
| Configuration | {n} | {n} | {n} |
| **Total** | **{n}** | **{n}** | **{n}** |

Copilot tokens used: {total}

### Changes by Category

#### Project Files
| File | Status |
|------|--------|
| `src/Module1/Module1.csproj` | ✅ Updated |

#### Code Samples
| File | Status |
|------|--------|
| `src/Module1/Program.cs` | ✅ Updated |

#### Documentation
| File | Status |
|------|--------|
| `docs/module-01.md` | ✅ Updated |

{if failures}
### ⚠️ Failures

The following files could not be transformed and were **not included** in this PR:

| File | Error |
|------|-------|
| `src/Module3/Complex.cs` | Copilot API timeout after 30s |
{end if}

{if review_markers}
### 👀 Review Markers

Files containing `REVIEW:` markers that need human attention:

- `src/Module2/Startup.cs` — line 42
- `docs/module-02.md` — line 18
{end if}

### Notes
- This PR was generated by WorkshopManager in response to #{issue-number}
- Review carefully before merging
- {n} file(s) required no changes (already at target version)

---
Closes #{issue-number}
```

### 6.4 Idempotency

If the orchestrator is invoked again for the same issue:

1. **Branch exists?** — Delete and recreate from current default branch HEAD. This ensures the branch reflects the latest base state.
2. **PR exists for branch?** — Update the existing PR body and title rather than creating a duplicate.
3. **Detection:** Query `GET /repos/{owner}/{repo}/pulls?head={owner}:{branch}&state=open` to find existing PRs.

---

## 7. Error Handling Strategy

### 7.1 Per-File Fault Isolation

The core principle: **a file-level failure is captured, not thrown.** Both transformation services catch exceptions per-file and return a `TransformationResult` with `Success = false`.

```csharp
// Inside CodeTransformationService.TransformAsync (per-file loop)
foreach (var item in items)
{
    try
    {
        var content = await _contentProvider.GetFileContentAsync(repo, sha, item.Path, ct);
        var skillPath = _skillResolver.ResolveSkillPath(item.Type, intent.Scope);
        var context = BuildContext(item, intent, structure);
        var response = await _copilotClient.TransformContentAsync(content, skillPath, context, ct);

        results.Add(new TransformationResult
        {
            Path = item.Path,
            ContentType = item.Type,
            Success = response.Success,
            TransformedContent = response.Success ? response.TransformedContent : null,
            OriginalContent = content,
            ErrorMessage = response.ErrorMessage,
            TokensUsed = response.TokensUsed
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to transform {Path}", item.Path);
        results.Add(new TransformationResult
        {
            Path = item.Path,
            ContentType = item.Type,
            Success = false,
            OriginalContent = string.Empty,
            ErrorMessage = $"Unexpected error: {ex.Message}",
            TokensUsed = 0
        });
    }
}
```

### 7.2 Failure Escalation Rules

| Condition | Behavior |
|-----------|----------|
| All files succeed | Normal PR |
| Some files fail, some succeed | PR with successful changes + failure table in description |
| All files fail | No PR created. Comment on issue explaining failures. |
| Workshop analysis fails | No PR. Comment on issue: "Could not analyze workshop structure." |
| PR generation fails (after successful transforms) | Comment on issue with transformation summary; suggest manual branch creation. |
| Copilot API rate limited | Retry with exponential backoff (3 attempts, 2s/4s/8s). Then fail the file. |
| File not found during transform | Fail that file (stale WorkshopStructure). Log warning. |

### 7.3 Partial Success Reporting

Failed files are:
1. **NOT committed** to the PR branch — only successfully transformed files are included.
2. **Listed in the PR description** under the "⚠️ Failures" section with error messages.
3. **Logged** at `Warning` level with structured logging (file path, error, content type).

The PR description always includes the full truth: what changed, what failed, and what was already up to date. This lets the workshop author decide whether to merge a partial upgrade or fix failures manually first.

---

## 8. Documentation vs. Code: Prompt Strategy Differences

### 8.1 Code Transformation Prompt Strategy

The `upgrade-code-sample.md` skill sends Copilot:
- **System prompt:** Full skill template with `{{technology}}`, `{{fromVersion}}`, `{{toVersion}}` hydrated.
- **User message:** The raw file content with "Transform the following content" prefix.
- **Key instructions:** Preserve pedagogical comments, use REVIEW markers for uncertain changes, maintain the file's teaching intent.

### 8.2 Documentation Transformation Prompt Strategy

The `upgrade-documentation.md` skill differs in these ways:

1. **Context enrichment** — The documentation transformer can optionally include file content from related code files (same module group) so Copilot understands what the prose is describing.
2. **Instruction emphasis** — Preserving narrative flow and instructional scaffolding. Code snippets embedded in markdown must match the transformed code files.
3. **Version reference updates** — Explicit instruction to find and update version numbers, download URLs, CLI commands, and API names in prose.
4. **Lighter touch** — Documentation transformation should change as little as possible. Only update parts that directly reference the technology being upgraded.

### 8.3 When to Use One vs. the Other

| Content Type | Service | Skill File | Rationale |
|-------------|---------|------------|-----------|
| `CodeSample` | Code | `upgrade-code-sample.md` | Full code transformation |
| `ProjectFile` | Code | `upgrade-project-file.md` | Structured XML/JSON update |
| `Configuration` | Code | `upgrade-project-file.md` | Config file update |
| `Documentation` | Docs | `upgrade-documentation.md` | Prose transformation |
| `Asset` | *(skipped)* | — | Not transformed in v1 |

---

## 9. Service Registration (DI)

All Phase 3 services register in `Program.cs` or an extension method:

```csharp
// In Program.cs or a ServiceCollectionExtensions.cs

public static IServiceCollection AddPhase3Services(this IServiceCollection services)
{
    // Transformation services
    services.AddScoped<ICodeTransformationService, CodeTransformationService>();
    services.AddScoped<IDocumentationTransformationService, DocumentationTransformationService>();

    // PR generation
    services.AddScoped<IPullRequestService, PullRequestService>();

    // Top-level orchestrator
    services.AddScoped<IUpgradeOrchestrator, UpgradeOrchestrator>();

    return services;
}
```

**Why `Scoped`?**

- Each webhook invocation creates a scope. The Octokit installation client (in `GitHubContentProvider`) caches auth tokens per scope. Transformation services should share that scope to reuse the authenticated client.
- `Singleton` would risk stale auth tokens. `Transient` would waste token negotiation.

**Dependencies already registered:**

| Service | Registration | Phase |
|---------|-------------|-------|
| `IWorkshopAnalyzer` / `WorkshopAnalyzer` | Scoped | Phase 2 |
| `IRepositoryContentProvider` / `GitHubContentProvider` | Scoped | Phase 2 |
| `ICopilotClient` / `CopilotClient` | Scoped | Phase 1 |
| `ISkillResolver` / `SkillResolver` | Singleton | Phase 2 |
| `FileClassifier` | Singleton | Phase 2 |
| `TechnologyDetector` | Scoped | Phase 2 |

---

## 10. Key Decisions

### D1: Separate Code and Documentation Transformation Services

**Decision:** Two distinct services (`ICodeTransformationService`, `IDocumentationTransformationService`) rather than one unified `ITransformationService`.

**Rationale:** Different prompt strategies, different context needs (docs may need related code), different failure severity. The cost is one additional interface; the benefit is cleaner testing and the ability to evolve each independently.

**Alternative rejected:** Single `ITransformationService` that internally branches on content type. This hides the distinction and makes mocking harder for Kate.

### D2: TransformationResult Per File, Not Per Batch

**Decision:** Each file produces its own `TransformationResult`. The `TransformationSummary` aggregates them.

**Rationale:** Per-file granularity enables: (a) partial success reporting in the PR, (b) per-file retry in the future, (c) clear token accounting. A batch-level result would hide individual failures.

### D3: Sequential Copilot Calls, Not Parallel

**Decision:** Transform files sequentially within each service.

**Rationale:** Copilot API rate limits are not well-documented. Sequential calls are predictable and debuggable. We can introduce controlled parallelism (e.g., `SemaphoreSlim(3)`) once we understand rate limit behavior in production.

### D4: Git Data API for Commits, Not Push

**Decision:** Use GitHub Git Data API (blobs → trees → commits → refs) to create multi-commit branches without a local clone.

**Rationale:** Consistent with the remote-first principle from Phase 2. No disk dependency means simpler hosting (Azure Functions compatible). The Git Data API gives us precise control over commit structure, which is essential for the multi-commit strategy.

### D5: Branch Recreate on Retry, Not Merge

**Decision:** If the branch already exists when re-running, delete and recreate it from current default branch HEAD.

**Rationale:** Force-updating ensures the PR always reflects a clean transformation from the latest base state. Merging base into the existing branch could create confusing merge commits. This is safe because the branch is automation-owned — no human commits expected on it.

### D6: Failed Files Excluded from PR

**Decision:** Files that fail transformation are NOT committed. They appear only in the PR description's failure table.

**Rationale:** Committing unchanged originals would mislead reviewers into thinking the file was reviewed by Copilot. Committing partial/broken output is worse. Clean separation: PR contains only validated changes. Failures are documented for manual follow-up.

### D7: UpgradeOrchestrator Owns the Pipeline

**Decision:** A single `IUpgradeOrchestrator` service orchestrates the full pipeline from `UpgradeIntent` to `UpgradeResult`.

**Rationale:** The webhook handler should not contain business logic. A dedicated orchestrator is testable (mock the four dependencies), composable (can be called from webhook handler, CLI, or test harness), and provides a single entry point for the entire workflow.

---

## 11. New Files for Phase 3

```
src/WorkshopManager.Api/
├── Models/
│   ├── TransformationResult.cs    ← NEW
│   ├── TransformationSummary.cs   ← NEW
│   ├── PullRequestResult.cs       ← NEW
│   └── UpgradeResult.cs           ← NEW (UpgradeResult, UpgradePhase)
├── Services/
│   ├── ICodeTransformationService.cs            ← NEW (WI-15 interface)
│   ├── CodeTransformationService.cs             ← NEW (WI-15 implementation, Riri)
│   ├── IDocumentationTransformationService.cs   ← NEW (WI-16 interface)
│   ├── DocumentationTransformationService.cs    ← NEW (WI-16 implementation, Riri)
│   ├── IPullRequestService.cs                   ← NEW (WI-17 interface)
│   ├── PullRequestService.cs                    ← NEW (WI-17 implementation, America)
│   ├── IUpgradeOrchestrator.cs                  ← NEW (orchestrator interface)
│   └── UpgradeOrchestrator.cs                   ← NEW (orchestrator implementation)
```

---

## 12. Open Questions

| # | Question | Owner | Notes |
|---|----------|-------|-------|
| 1 | Should documentation transformation include related code files as context? | Kamala | Proposed yes for files in the same Group. Deferred to implementation — Riri to assess token budget. |
| 2 | Copilot rate limit behavior under sustained load? | Riri | Unknown until production testing. Sequential processing is the safe default. |
| 3 | Should we cache TransformationResults per commit SHA + intent? | Kamala | Probably yes for retries. Defer to Phase 4 polish. |
| 4 | PR description max length? | America | GitHub API limit is 65536 chars. May need truncation for large workshops. |
| 5 | Should build validation (WI-18) run before or after PR creation? | Kamala | After — create draft PR, validate, then mark ready. Keeps the PR as progress record even if build fails. |

---

## 13. Team Routing

| Work Item | Owner | Dependencies | This Design Provides |
|-----------|-------|-------------|---------------------|
| **WI-15** (Code transformation) | Riri | WI-12, WI-14 | `ICodeTransformationService` interface, `TransformationResult` model, error handling pattern |
| **WI-16** (Docs transformation) | Riri | WI-12, WI-14 | `IDocumentationTransformationService` interface, prompt strategy guidance |
| **WI-17** (PR generation) | America | WI-15, WI-16 | `IPullRequestService` interface, commit strategy, PR template, branch naming, Git Data API approach |
| **WI-18** (Build validation) | Riri | WI-15 | Integration point: runs after PR creation on draft PR |
| **WI-19** (E2E tests) | Kate | WI-17 | Full interface set for mocking, `UpgradeOrchestrator` as test entry point |
