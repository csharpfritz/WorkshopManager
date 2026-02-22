# Project Context

- **Owner:** Jeffrey T. Fritz
- **Project:** GitHub App that attaches to workshop repos, analyzes issues requesting tech upgrades, uses Copilot to review content, updates code samples and prose, and delivers PRs with changes.
- **Stack:** C#, .NET, GitHub Apps, GitHub Copilot API, Webhooks
- **Created:** 2026-02-13

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

📌 Team update (2026-02-13): Copilot SDK for .NET will be used with custom SKILL.md prompts for content transformation and analysis; convention-based workshop detection with optional .workshop.yml manifest for edge cases — decided by Kamala

📌 Team update (2026-02-13): ICopilotClient interface with stub implementation — Phase 1 stub returns content unchanged; real SDK integration in Phase 2; interface contract stable regardless of implementation; CopilotResponse (4 fields) and CopilotContext (5 fields) records established — decided by Kamala

📌 Team update (2026-02-14): Release Notes and Dependabot Integration extends existing architecture — Trigger Classifier routes upgrade intent; both new triggers call Upgrade Processor; no changes to core Copilot integration or content analysis workflows; Phase 5 feature (WI-26 to WI-38) — decided by Kamala

📌 WI-04/05 shipped (2026-02-14): Shared models and interfaces complete — UpgradeIntent, CopilotResponse, CopilotContext records; IIssueParser, ICopilotClient interfaces; IssueParser regex implementation with GeneratedRegex source generators; StubCopilotClient pass-through; bot name detection from config; technology/scope/release notes parsing — decided by Riri

📌 Team update (2026-02-14): IssueParser regex bug documented — multi-word version strings (e.g., `.NET 9`) captured as single token (e.g., `.NET`); affects `BodyToFieldPattern`, `BodyFromFieldPattern`, `TitleFromToPattern`; recommendation to use `(\S+(?:\s+\S+)?)` or anchored pattern; fixtures document actual vs. expected behavior per protocol — documented by Kate

## 2026-02-13  WI-10: Manifest-based content discovery

Implemented manifest-based content discovery system with hybrid fallback logic.

**Architecture decisions:**
- ManifestParser uses YamlDotNet with camelCase naming convention for automatic YAMLC# mapping
- Parser returns null on YAML errors rather than throwing, allowing graceful fallback to conventions
- WorkshopAnalyzer checks for .workshop.yml before applying convention-based detection
- DetectionStrategy enum tracks how structure was discovered (Manifest/Convention/Hybrid)

**Technology integration:**
- Added YamlDotNet NuGet package for YAML parsing
- Integrated ManifestParser into WorkshopAnalyzer's constructor via DI
- Extended ContentItemType enum to include Asset type for static resources

**Hybrid fallback logic per design doc section 4.3:**
- Manifest technology overrides convention-detected technology when present
- Manifest structure.modules determines grouping; conventions used if missing
- Partial manifests trigger Hybrid strategy; complete manifests use Manifest strategy
- Empty/missing manifests use full Convention strategy

**Key file paths:**
- Services/ManifestParser.cs  YAML parsing with YamlDotNet
- Services/WorkshopAnalyzer.cs  orchestrates manifest + convention detection
- Models/WorkshopManifest.cs  typed manifest representation
- Models/ContentItemType.cs  updated with Asset enum value

Build verified successful. Ready for WI-09 integration (if not already complete).

## 2026-02-14  WI-09: Convention-based content discovery

Implemented convention-based workshop structure detection with file classification, technology detection, and group assignment.

**Architecture decisions:**
- Separated concerns into FileClassifier (pattern matching) and TechnologyDetector (version extraction)
- TechnologyDetector depends on IRepositoryContentProvider for file content access
- WorkshopAnalyzer orchestrates FileClassifier + TechnologyDetector + version reference extraction
- GeneratedRegex used throughout for performance (compile-time regex generation)

**File classification strategy (FileClassifier.cs):**
- Pattern-based classification by extension and filename
- Directory pattern detection for module/lab/exercise/chapter structures
- Automatic exclusion of build artifacts (.git/, node_modules/, bin/, obj/, etc.)
- Supports manifest-defined exclusion paths
- Group inference from numbered section directories (module-01, lab01, etc.)
- Sorting priority: ProjectFile > CodeSample > Documentation > Configuration > Asset

**Technology detection priority (TechnologyDetector.cs):**
- .NET: .csproj/.fsproj TargetFramework + global.json SDK version
- Node.js: package.json engines.node + .nvmrc
- Python: pyproject.toml requires-python + .python-version + requirements.txt presence
- Go: go.mod go directive
- Java: pom.xml java.version + build.gradle sourceCompatibility
- First match wins for primary technology; version references extracted per project file

**Version reference extraction:**
- ExtractVersionReferencesAsync populates VersionReference list per ContentItem
- Each project file analyzed for framework/runtime versions
- Location field tracks which file the version came from
- Supports multiple version references per item (e.g., SDK + runtime)

**IssueParser regex fix (Kate's bug report):**
- Changed all `(\S+)` capture groups to `(.+?)` with proper anchors
- Affected patterns: TitleTargetPattern, TitleFromToPattern, BodyToFieldPattern, BodyFromFieldPattern, BodyScopeFieldPattern
- Fix supports multi-word versions like ".NET 9" and "Node.js 20"
- Uses non-greedy lazy quantifier `+?` with line-end anchors `(?:\r?\n|$)` or word boundary `(?:\s|$)`

**InMemoryContentProvider for testing:**
- Dictionary-backed IRepositoryContentProvider implementation
- AddFile/AddFiles for fixture setup, Clear for teardown
- Returns RepositoryFile list and file content by path
- FileNotFoundException on missing paths (matches production behavior)

**DI registration in Program.cs:**
- FileClassifier (singleton)
- TechnologyDetector (singleton)
- IWorkshopAnalyzer → WorkshopAnalyzer (singleton)
- Note: IRepositoryContentProvider not yet registered (WI-11 will add GitHub API impl)

**Key file paths:**
- Models/WorkshopStructure.cs  top-level analysis result
- Models/ContentItem.cs  individual file classification + version refs
- Models/WorkshopManifest.cs  manifest type structure
- Services/IWorkshopAnalyzer.cs  main analysis interface
- Services/IRepositoryContentProvider.cs  abstraction for repo access
- Services/WorkshopAnalyzer.cs  orchestrates analysis with logging
- Services/FileClassifier.cs  pattern-based file classification
- Services/TechnologyDetector.cs  version extraction logic
- Services/InMemoryContentProvider.cs  testing implementation

**Build status:** Successful (2 warnings about NU1510 package pruning — non-blocking)

Implementation complete. Kate will write tests in WI-13. Manifest parsing stub in WorkshopAnalyzer ready for WI-10 integration (ManifestParser).

## 2026-02-14  WI-12: Copilot Analysis Service

Implemented the real Copilot client integration replacing the stub implementation.

**Architecture decisions:**
- CopilotClient uses IHttpClientFactory for HttpClient instance management
- Skill templates loaded from disk via File.ReadAllTextAsync (skills in AppContext.BaseDirectory/Skills/)
- Placeholder hydration uses case-insensitive string replacement for {{technology}}, {{fromVersion}}, {{toVersion}}, {{releaseNotesUrl}}
- Error handling returns CopilotResponse with Success=false rather than throwing exceptions
- ValidateConnectionAsync pings /v1/health endpoint to verify credentials

**SkillResolver routing logic (already implemented):**
- UpgradeScope.Incremental → analyze-breaking-changes.md (any content type)
- ContentItemType.CodeSample → upgrade-code-sample.md
- ContentItemType.Documentation → upgrade-documentation.md
- ContentItemType.ProjectFile → upgrade-project-file.md
- ContentItemType.Configuration → upgrade-project-file.md
- Throws ArgumentOutOfRangeException for unsupported content types

**Copilot API integration:**
- POST /v1/chat/completions with system (skill prompt) + user (content) messages
- Uses CopilotSettings from config: ApiEndpoint, ApiKey, Model, MaxTokens, TimeoutSeconds
- Authorization via Bearer token in request headers
- Response parsed into internal CompletionResponse DTOs
- Token usage extracted from response.Usage.TotalTokens

**Error handling strategy:**
- FileNotFoundException → skill file not found, returns failure response
- HttpRequestException → API communication failure, logged with details
- TaskCanceledException → timeout (TimeoutSeconds from config), logged
- Generic Exception → unexpected error, logged and returned as failure
- All errors return original content unchanged with Success=false

**DI registration in Program.cs:**
- AddHttpClient() for IHttpClientFactory
- ISkillResolver → SkillResolver (singleton)
- ICopilotClient → CopilotClient (singleton, replacing StubCopilotClient)

**Key file paths:**
- Services/CopilotClient.cs  real Copilot API integration
- Services/ISkillResolver.cs  skill routing interface (pre-existing)
- Services/SkillResolver.cs  skill routing implementation (pre-existing)
- Program.cs  DI wiring for Copilot services
- WorkshopManager.Api.csproj  added Skills/*.md CopyToOutputDirectory

**Build status:** Successful (2 warnings about NU1510 package pruning — non-blocking)

Skills directory deployment verified — all four .md files copied to bin/Debug/net10.0/Skills/.

Implementation complete. Kate will write tests in WI-13 to validate skill loading, placeholder hydration, and API integration behavior.

## 2026-02-14  WI-11: GitHubContentProvider — Real IRepositoryContentProvider

Implemented the production `IRepositoryContentProvider` backed by the GitHub API via Octokit.NET v14.

**Architecture decisions:**
- GitHubContentProvider authenticates as a GitHub App installation per-repo
- JWT generation uses built-in `System.Security.Cryptography` RSA (no extra JWT library)
- Installation token cached within scoped lifetime to avoid repeated auth calls during a single request
- `GetFileTreeAsync` uses Git Tree API (`client.Git.Tree.GetRecursive`) for full recursive tree
- `GetFileContentAsync` uses Contents API (`client.Repository.Content.GetRawContentByRef`) for raw file bytes
- Truncated tree responses logged as warning but still returned (large repos)

**DI lifetime changes:**
- `IRepositoryContentProvider` → `GitHubContentProvider` registered as **scoped** (per-request auth)
- `TechnologyDetector` changed to **scoped** (depends on scoped content provider)
- `IWorkshopAnalyzer` → `WorkshopAnalyzer` changed to **scoped** (depends on scoped services)
- `FileClassifier` and `IManifestParser` remain singleton (no scoped dependencies)
- `InMemoryContentProvider` remains as concrete class for test DI overrides

**Error handling strategy:**
- `NotFoundException` → repo/file/commit not found (logged, re-thrown as InvalidOperationException or FileNotFoundException)
- `AuthorizationException` → bad AppId/PrivateKey (logged with config hint)
- `RateLimitExceededException` → primary rate limit (logged with reset time)
- `ForbiddenException` → secondary rate limit or permission issue
- `ApiException` → catch-all for other GitHub API errors
- All errors logged before wrapping to preserve Octokit exception details

**GitHub App auth flow:**
1. Generate RS256 JWT from AppId + PrivateKey (PEM) with 10-min expiry
2. Use JWT to call `GetRepositoryInstallationForCurrent(owner, repo)`
3. Create installation access token via `CreateInstallationToken(installationId)`
4. Cache authenticated `GitHubClient` for the scoped lifetime

**Key file paths:**
- Services/GitHubContentProvider.cs — production implementation
- Services/InMemoryContentProvider.cs — test implementation (unchanged)
- Services/IRepositoryContentProvider.cs — interface (unchanged)
- Configuration/GitHubAppOptions.cs — AppId, PrivateKey, WebhookSecret, AppName
- Program.cs — DI registration updated

**NuGet packages added:**
- `Octokit` v14.0.0

**Build status:** Successful (pre-existing NU1510 warning only)
**Test status:** All 11 integration tests pass. 119/124 unit tests pass (5 pre-existing IssueParser regex failures — documented bug).

## 2026-02-22  Milestone 1 Complete — Phase 1 Stabilize

All 135 tests passing. Team converged on three parallel streams:
- **Kate (Tester):** Fixed 5 unit test bugs (YAML quoting, case-insensitive YAML matching, regex for multi-word versions, assertion corrections)
- **America (Frontend Dev):** Fixed DI registration bug (IManifestParser missing from Program.cs)
- **Riri (Backend Dev):** Implemented real GitHubContentProvider with Octokit v14.0.0 and scoped lifetime cascade

**Outcome:** Full webhook → Parser → Analyzer → Copilot pipeline now operational. 124/124 unit tests + 11/11 integration tests passing. Phase 2 ready: release notes integration, Dependabot trigger, content transformation at scale.
