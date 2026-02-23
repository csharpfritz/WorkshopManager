# Workshop Structure Detection — Design Document

> **WI-08** | **Author:** Kamala (Lead) | **Date:** 2026-02-14  
> **Status:** Proposed  
> **Implements:** PRD Section 6 — Content Analysis Engine

---

## 1. Overview

The workshop structure detection system discovers how a repository organizes its workshop content — code samples, documentation, configuration files, and their relationships. It produces a `WorkshopStructure` model that downstream services (Copilot analysis, transformation, PR generation) consume.

**Two detection strategies**, applied in order:

1. **Manifest-based** — If `.workshop.yml` exists at the repo root, parse it as the authoritative structure definition
2. **Convention-based** — Scan directory and file patterns to infer structure automatically

If a manifest is present but incomplete (e.g., declares modules but omits technology), convention-based detection fills the gaps.

### Integration Point

```
IssueParser → UpgradeIntent → WorkshopAnalyzer → WorkshopStructure → Copilot transforms each ContentItem
```

The `WorkshopAnalyzer` sits between intent parsing (Phase 1) and content transformation (Phase 3). It answers: *"What files exist in this repo, what role does each play, and what technology/version do they target?"*

---

## 2. Data Model

### 2.1 WorkshopStructure

Top-level record representing a fully analyzed workshop repository.

```csharp
namespace WorkshopManager.Models;

/// <summary>
/// Represents the discovered structure of a workshop repository.
/// Produced by IWorkshopAnalyzer, consumed by transformation services.
/// </summary>
public record WorkshopStructure
{
    /// <summary>
    /// Root path of the workshop within the repository (usually "/").
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Primary technology detected (e.g., "dotnet", "node", "python").
    /// </summary>
    public required string Technology { get; init; }

    /// <summary>
    /// Detected version of the primary technology (e.g., "8.0", "20").
    /// Null if version could not be determined.
    /// </summary>
    public string? TechnologyVersion { get; init; }

    /// <summary>
    /// The parsed manifest, if .workshop.yml was found. Null for pure convention-based detection.
    /// </summary>
    public WorkshopManifest? Manifest { get; init; }

    /// <summary>
    /// All discovered content items in the workshop.
    /// </summary>
    public required IReadOnlyList<ContentItem> Items { get; init; }

    /// <summary>
    /// Detection strategy that produced this structure.
    /// </summary>
    public required DetectionStrategy Strategy { get; init; }

    /// <summary>
    /// Warnings or notes from the detection process (e.g., "No project files found").
    /// </summary>
    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}

public enum DetectionStrategy
{
    /// <summary>Structure derived entirely from .workshop.yml manifest.</summary>
    Manifest,

    /// <summary>Structure inferred from directory/file conventions.</summary>
    Convention,

    /// <summary>Manifest provided partial info; conventions filled gaps.</summary>
    Hybrid
}
```

### 2.2 ContentItem

Represents an individual file or logical section discovered in the workshop.

```csharp
namespace WorkshopManager.Models;

/// <summary>
/// A single content item (file) discovered in a workshop repository.
/// </summary>
public record ContentItem
{
    /// <summary>
    /// Path relative to the repository root (e.g., "src/Module01/Program.cs").
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Classification of this content item.
    /// </summary>
    public required ContentItemType Type { get; init; }

    /// <summary>
    /// Technology detected for this specific item, if different from the workshop-level technology.
    /// Null means "inherits from WorkshopStructure.Technology".
    /// </summary>
    public string? Technology { get; init; }

    /// <summary>
    /// Version references found in this file (e.g., TargetFramework value, engine version).
    /// Empty if no version information detected.
    /// </summary>
    public IReadOnlyList<VersionReference> VersionReferences { get; init; } = [];

    /// <summary>
    /// Dependencies declared in this file (e.g., NuGet packages, npm packages).
    /// Only populated for project/config files that declare dependencies.
    /// </summary>
    public IReadOnlyList<DependencyReference> Dependencies { get; init; } = [];

    /// <summary>
    /// Logical group this item belongs to (e.g., "module-01", "lab-03").
    /// Null if the item is at the workshop root level.
    /// </summary>
    public string? Group { get; init; }
}

public enum ContentItemType
{
    /// <summary>Source code file (*.cs, *.ts, *.py, *.ps1, *.sh).</summary>
    CodeSample,

    /// <summary>Markdown or text documentation (*.md, *.txt).</summary>
    Documentation,

    /// <summary>Project/build file (*.csproj, *.sln, package.json, pyproject.toml).</summary>
    ProjectFile,

    /// <summary>Configuration file (*.json, *.yml, *.yaml, Dockerfile, devcontainer.json).</summary>
    Configuration,

    /// <summary>Static asset referenced by the workshop but not directly transformed.</summary>
    Asset
}

/// <summary>
/// A technology version reference found within a file.
/// </summary>
public record VersionReference(
    string FrameworkOrRuntime,
    string Version,
    string Location);

/// <summary>
/// A dependency declared in a project or config file.
/// </summary>
public record DependencyReference(
    string Name,
    string Version,
    string? Source);
```

### 2.3 WorkshopManifest

Typed representation of a parsed `.workshop.yml` file.

```csharp
namespace WorkshopManager.Models;

/// <summary>
/// Typed representation of a .workshop.yml manifest file.
/// </summary>
public record WorkshopManifest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public ManifestTechnology? Technology { get; init; }
    public ManifestStructure? Structure { get; init; }
}

public record ManifestTechnology
{
    public required string Primary { get; init; }
    public string? Version { get; init; }
    public IReadOnlyList<string> Additional { get; init; } = [];
}

public record ManifestStructure
{
    public IReadOnlyList<ManifestModule> Modules { get; init; } = [];
    public IReadOnlyList<string> Shared { get; init; } = [];
    public IReadOnlyList<string> Exclude { get; init; } = [];
}

public record ManifestModule
{
    public required string Path { get; init; }
    public string? Code { get; init; }
    public string? Docs { get; init; }
    public string? Title { get; init; }
    public int? Order { get; init; }
}
```

---

## 3. Convention-Based Detection Algorithm

When no manifest is present (or the manifest is incomplete), the analyzer infers structure from filesystem patterns.

### 3.1 Directory Pattern Recognition

The following directory names, when found at the repo root or one level deep, indicate workshop organizational structure:

| Pattern | Interpretation | Content type assigned |
|---------|---------------|---------------------|
| `src/`, `code/`, `samples/` | Code sample container | Children → `CodeSample` |
| `docs/`, `instructions/`, `content/` | Documentation container | Children → `Documentation` |
| `exercises/`, `labs/` | Lab/exercise modules | Children → mixed (scan each) |
| `modules/`, `steps/`, `chapters/` | Sequential workshop sections | Children → mixed (scan each) |
| `chapter*/`, `module*/`, `lab*/`, `step*/` | Numbered section directories | Children → mixed (scan each) |
| `shared/`, `common/`, `assets/` | Shared assets | Children → `Asset` |

### 3.2 File Pattern Classification

Files are classified by extension and name:

| Pattern | ContentItemType | Notes |
|---------|----------------|-------|
| `*.cs`, `*.fs` | CodeSample | .NET source |
| `*.ts`, `*.js`, `*.tsx`, `*.jsx` | CodeSample | JavaScript/TypeScript |
| `*.py` | CodeSample | Python |
| `*.ps1`, `*.sh`, `*.bash` | CodeSample | Scripts |
| `*.csproj`, `*.fsproj`, `*.sln` | ProjectFile | .NET project files |
| `package.json` | ProjectFile | Node.js project |
| `requirements.txt`, `pyproject.toml`, `setup.py` | ProjectFile | Python project |
| `*.md`, `*.txt` (in docs directories) | Documentation | Prose content |
| `README.md`, `WORKSHOP.md` | Documentation | Workshop entry points (any location) |
| `Dockerfile`, `docker-compose.yml` | Configuration | Container config |
| `devcontainer.json`, `.devcontainer/` | Configuration | Dev environment |
| `*.json`, `*.yml`, `*.yaml` | Configuration | General config (not matching above) |
| `*.png`, `*.jpg`, `*.gif`, `*.svg` | Asset | Image assets |

### 3.3 Technology + Version Detection

Technology detection is **ordered by specificity** (most specific match wins):

```
1. .NET:     *.csproj → parse <TargetFramework> (e.g., "net8.0" → technology="dotnet", version="8.0")
             *.fsproj → same parsing
             global.json → parse sdk.version

2. Node.js:  package.json → parse engines.node (e.g., ">=20" → technology="node", version="20")
             .nvmrc → parse version string

3. Python:   pyproject.toml → parse [project].requires-python
             .python-version → parse version string
             requirements.txt → presence indicates Python (version from runtime file)

4. Go:       go.mod → parse go directive (e.g., "go 1.22" → technology="go", version="1.22")

5. Java:     pom.xml → parse java.version property
             build.gradle → parse sourceCompatibility
```

**Algorithm:**
1. Walk the repo root for project files in priority order above
2. First match determines `WorkshopStructure.Technology` and `TechnologyVersion`
3. Each project file found also produces `VersionReference` entries on its `ContentItem`
4. If multiple technologies detected, primary = first match; others recorded as `ContentItem.Technology` overrides

### 3.4 Building the Content Item Tree

```
1. Walk all files in the repository (respecting .gitignore)
2. Exclude: .git/, node_modules/, bin/, obj/, .vs/, __pycache__/
3. For each file:
   a. Classify by file pattern → ContentItemType
   b. Determine group from parent directory (if parent matches a module pattern)
   c. Extract version references (if ProjectFile or Configuration)
   d. Extract dependencies (if ProjectFile)
4. Sort items: ProjectFile first, then CodeSample, Documentation, Configuration, Asset
```

### 3.5 Excluded Paths

Always excluded from analysis (not configurable in v1):

```
.git/
node_modules/
bin/
obj/
.vs/
__pycache__/
.workshop.yml (the manifest itself is parsed, not treated as content)
```

---

## 4. Manifest-Based Detection

### 4.1 `.workshop.yml` Schema

```yaml
# .workshop.yml — Workshop structure manifest
# All fields are optional. Convention-based detection fills gaps.

name: "Intro to ASP.NET Core"           # Workshop display name
description: "Learn ASP.NET Core basics" # Optional description

technology:
  primary: "dotnet"                      # Required if technology section present
  version: "8.0"                         # Current version (what the workshop targets now)
  additional:                            # Other technologies used (optional)
    - "docker"
    - "typescript"

structure:
  modules:                               # Ordered list of workshop modules
    - path: "module-01-setup"            # Required: directory path relative to repo root
      code: "src/"                       # Optional: code subdirectory within module
      docs: "instructions.md"            # Optional: documentation file/directory within module
      title: "Environment Setup"         # Optional: human-readable title
      order: 1                           # Optional: explicit ordering (default: list position)

    - path: "module-02-routing"
      code: "src/"
      docs: "instructions.md"
      title: "Routing Basics"
      order: 2

  shared:                                # Paths to shared assets (not module-specific)
    - "shared-assets/"
    - "global-config/"

  exclude:                               # Paths to exclude from analysis
    - "archived/"
    - "instructor-notes/"
```

### 4.2 Manifest → WorkshopStructure Mapping

```
.workshop.yml field          →  WorkshopStructure field
──────────────────────────────────────────────────────────
name                         →  (informational, stored in Manifest)
technology.primary           →  Technology
technology.version           →  TechnologyVersion
structure.modules[].path     →  ContentItem.Group
structure.modules[].code     →  scan path/code/ → ContentItems with Group
structure.modules[].docs     →  scan path/docs  → ContentItems with Group
structure.shared[]           →  scan shared paths → ContentItems (Group = null)
structure.exclude[]          →  skip these paths entirely
```

### 4.3 Hybrid Fallback Logic

When a manifest is present but partial:

| Manifest provides | Convention fills |
|-------------------|-----------------|
| `technology` only | Scan for modules via directory patterns; classify all files |
| `structure.modules` only | Detect technology from project files within declared modules |
| `structure.modules` without `code`/`docs` | Scan each module directory, classify files by extension |
| Nothing (empty manifest) | Full convention-based detection; `Strategy = Convention` |
| Everything | Pure manifest; `Strategy = Manifest` |

The `Strategy` field on `WorkshopStructure` records which approach was used:
- `Manifest` — manifest provided all needed information
- `Convention` — no manifest, or manifest was empty/missing
- `Hybrid` — manifest provided some info, conventions filled gaps

---

## 5. Service Interface

### 5.1 IWorkshopAnalyzer

```csharp
using WorkshopManager.Models;

namespace WorkshopManager.Services;

/// <summary>
/// Analyzes a repository to discover its workshop structure.
/// Phase 2: operates on repository contents fetched via GitHub API.
/// </summary>
public interface IWorkshopAnalyzer
{
    /// <summary>
    /// Analyze a repository's workshop structure.
    /// </summary>
    /// <param name="repoFullName">Repository in "owner/repo" format (e.g., "csharpfritz/workshop-dotnet").</param>
    /// <param name="commitSha">The commit SHA to analyze (ensures consistency during analysis).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Discovered workshop structure.</returns>
    Task<WorkshopStructure> AnalyzeAsync(
        string repoFullName,
        string commitSha,
        CancellationToken ct = default);
}
```

### 5.2 Design Decision: GitHub Repo Reference, Not Local Path

The `AnalyzeAsync` method takes `repoFullName` + `commitSha`, not a local filesystem path. Rationale:

1. **Phase 2 operates on GitHub API** — We read repo contents via `Octokit.net` `GetAllContents()` and tree APIs, not local clones
2. **Consistency** — Pinning to a `commitSha` prevents TOCTOU issues where the repo changes during analysis
3. **No disk dependency** — The analyzer doesn't need filesystem access; this simplifies hosting (Azure Functions/App Service) and testing
4. **Aligns with downstream** — PR generation (Phase 3) also operates via GitHub API, so the entire pipeline stays remote

If future phases require local analysis (e.g., build validation in WI-18), that would be a separate service with its own interface, not a change to `IWorkshopAnalyzer`.

### 5.3 IRepositoryContentProvider

The analyzer needs to read files from GitHub. Rather than coupling directly to Octokit, we introduce a thin abstraction:

```csharp
namespace WorkshopManager.Services;

/// <summary>
/// Abstraction for reading repository contents. Backed by GitHub API in production,
/// in-memory or filesystem in tests.
/// </summary>
public interface IRepositoryContentProvider
{
    /// <summary>
    /// Get the repository file tree (paths only, no content).
    /// </summary>
    Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(
        string repoFullName, string commitSha, CancellationToken ct = default);

    /// <summary>
    /// Get the content of a specific file.
    /// </summary>
    Task<string> GetFileContentAsync(
        string repoFullName, string commitSha, string path, CancellationToken ct = default);
}

/// <summary>
/// Metadata about a file in the repository tree.
/// </summary>
public record RepositoryFile(string Path, string Type, long Size);
```

This abstraction allows:
- **Production:** Backed by Octokit Git Tree API (`GetTree(recursive: true)`) for file listing, Contents API for individual files
- **Testing:** In-memory implementation with predefined file trees and contents
- **Future:** Could swap to local filesystem if we add clone-based analysis

### 5.4 Orchestrated Analysis Flow

```csharp
// Pseudocode for WorkshopAnalyzer.AnalyzeAsync
public async Task<WorkshopStructure> AnalyzeAsync(string repoFullName, string commitSha, CancellationToken ct)
{
    // 1. Get the full file tree
    var files = await _contentProvider.GetFileTreeAsync(repoFullName, commitSha, ct);

    // 2. Check for manifest
    var manifestFile = files.FirstOrDefault(f => f.Path == ".workshop.yml");
    WorkshopManifest? manifest = null;
    if (manifestFile is not null)
    {
        var yaml = await _contentProvider.GetFileContentAsync(repoFullName, commitSha, ".workshop.yml", ct);
        manifest = _manifestParser.Parse(yaml);
    }

    // 3. Classify all files into ContentItems
    var items = _fileClassifier.ClassifyFiles(files, manifest?.Structure?.Exclude);

    // 4. Detect technology from project files
    var (technology, version) = await _techDetector.DetectAsync(
        items.Where(i => i.Type == ContentItemType.ProjectFile),
        repoFullName, commitSha, ct);

    // 5. Apply manifest overrides
    if (manifest?.Technology is not null)
    {
        technology = manifest.Technology.Primary;
        version = manifest.Technology.Version ?? version;
    }

    // 6. Apply module grouping from manifest
    if (manifest?.Structure?.Modules is { Count: > 0 } modules)
    {
        items = _groupAssigner.ApplyManifestGroups(items, modules);
    }
    else
    {
        items = _groupAssigner.InferGroups(items);
    }

    // 7. Determine strategy
    var strategy = (manifest, manifestFile) switch
    {
        (null, _) => DetectionStrategy.Convention,
        ({ Structure: not null, Technology: not null }, _) => DetectionStrategy.Manifest,
        _ => DetectionStrategy.Hybrid
    };

    return new WorkshopStructure
    {
        RootPath = "/",
        Technology = technology ?? "unknown",
        TechnologyVersion = version,
        Manifest = manifest,
        Items = items,
        Strategy = strategy
    };
}
```

---

## 6. Integration with Existing Code

### 6.1 Pipeline Position

```
Phase 1 (done):              Phase 2 (this design):           Phase 3 (future):
┌──────────────┐             ┌───────────────────┐            ┌────────────────────┐
│ IIssueParser │──────────►  │ IWorkshopAnalyzer │─────────►  │ Copilot transforms │
│              │             │                   │            │ each ContentItem   │
│ Produces:    │             │ Produces:         │            │                    │
│ UpgradeIntent│             │ WorkshopStructure │            │ Produces:          │
└──────────────┘             └───────────────────┘            │ Changed files      │
                                                              └────────────────────┘
```

### 6.2 Wiring with UpgradeIntent

The `UpgradeProcessor` (WI-09 implementation detail) orchestrates the handoff:

```csharp
public class UpgradeProcessor
{
    private readonly IWorkshopAnalyzer _analyzer;
    private readonly ICopilotClient _copilot;

    public async Task ProcessUpgradeAsync(UpgradeIntent intent, CancellationToken ct)
    {
        // Get the default branch HEAD SHA for consistent analysis
        var commitSha = await _githubClient.GetDefaultBranchHeadAsync(intent.RepoFullName, ct);

        // Analyze workshop structure
        var structure = await _analyzer.AnalyzeAsync(intent.RepoFullName, commitSha, ct);

        // Transform each content item that needs updating
        foreach (var item in structure.Items.Where(ShouldTransform))
        {
            var content = await _contentProvider.GetFileContentAsync(
                intent.RepoFullName, commitSha, item.Path, ct);

            var context = new CopilotContext(
                intent.RepoFullName,
                item.Path,
                intent.SourceVersion,
                intent.TargetVersion,
                item.Technology ?? structure.Technology);

            var result = await _copilot.TransformContentAsync(
                content, GetSkillPath(item.Type), context, ct);

            if (result.Success)
                _changes.Add(item.Path, result.TransformedContent);
        }

        // ... create PR with changes (Phase 3)
    }

    private static bool ShouldTransform(ContentItem item)
        => item.Type is ContentItemType.CodeSample
            or ContentItemType.ProjectFile
            or ContentItemType.Documentation
            or ContentItemType.Configuration;
}
```

### 6.3 Copilot Skill Routing

Each `ContentItemType` maps to a different SKILL.md prompt:

| ContentItemType | Skill File | Strategy |
|----------------|------------|----------|
| CodeSample | `skills/code-upgrade.md` | Full Copilot transformation |
| ProjectFile | `skills/project-upgrade.md` | XML/JSON parsing + Copilot |
| Documentation | `skills/docs-upgrade.md` | Copilot prose rewrite |
| Configuration | `skills/config-upgrade.md` | Schema-aware update |
| Asset | *(skipped)* | Not transformed in v1 |

### 6.4 New Files for Phase 2

```
src/WorkshopManager.Api/
├── Models/
│   ├── WorkshopStructure.cs      ← NEW (WorkshopStructure, DetectionStrategy)
│   ├── ContentItem.cs            ← NEW (ContentItem, ContentItemType, VersionReference, DependencyReference)
│   └── WorkshopManifest.cs       ← NEW (WorkshopManifest, ManifestTechnology, ManifestStructure, ManifestModule)
├── Services/
│   ├── IWorkshopAnalyzer.cs      ← NEW (interface)
│   ├── IRepositoryContentProvider.cs ← NEW (interface + RepositoryFile record)
│   ├── WorkshopAnalyzer.cs       ← NEW (WI-09 implementation)
│   ├── FileClassifier.cs         ← NEW (WI-09 convention logic)
│   ├── TechnologyDetector.cs     ← NEW (WI-09 version parsing)
│   └── ManifestParser.cs         ← NEW (WI-10 YAML parsing)
```

---

## 7. Testing Strategy

### 7.1 Unit Tests (Kate, WI-13)

| Test Area | Fixture Data | What to Assert |
|-----------|-------------|----------------|
| File classification | In-memory file trees | Correct `ContentItemType` for each pattern |
| Technology detection | Sample `.csproj`, `package.json`, etc. | Correct technology + version extraction |
| Manifest parsing | Sample `.workshop.yml` files | Correct `WorkshopManifest` hydration |
| Group assignment | Directory trees with module patterns | Correct `ContentItem.Group` values |
| Hybrid fallback | Partial manifests + file trees | `Strategy = Hybrid`, gaps filled correctly |

### 7.2 In-Memory Content Provider

```csharp
public class InMemoryContentProvider : IRepositoryContentProvider
{
    private readonly Dictionary<string, string> _files = new();

    public void AddFile(string path, string content) => _files[path] = content;

    public Task<IReadOnlyList<RepositoryFile>> GetFileTreeAsync(
        string repoFullName, string commitSha, CancellationToken ct)
    {
        var files = _files.Keys
            .Select(p => new RepositoryFile(p, "blob", _files[p].Length))
            .ToList();
        return Task.FromResult<IReadOnlyList<RepositoryFile>>(files);
    }

    public Task<string> GetFileContentAsync(
        string repoFullName, string commitSha, string path, CancellationToken ct)
    {
        return _files.TryGetValue(path, out var content)
            ? Task.FromResult(content)
            : throw new FileNotFoundException($"Test file not found: {path}");
    }
}
```

---

## 8. Open Questions

| # | Question | Owner | Notes |
|---|----------|-------|-------|
| 1 | Should `.workshop.yml` support glob patterns in `exclude`? | Kamala | v1: exact paths only. Glob support is a v2 nicety. |
| 2 | Rate limiting on GitHub Contents API for large repos? | America | Tree API returns up to 100K entries. Pagination needed? |
| 3 | Should we cache `WorkshopStructure` per commit SHA? | Kamala | Probably yes — same commit = same structure. Defer to implementation. |
| 4 | Binary file size threshold for `Asset` classification? | Kamala | v1: classify by extension, don't read content. Size irrelevant. |
