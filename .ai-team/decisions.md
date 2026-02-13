# Decisions

> Team decisions that all agents must respect. Managed by Scribe.

### 2026-02-13: Dual Trigger Mechanism
**By:** Kamala
**What:** WorkshopManager supports both label-based triggering (`workshop-upgrade` label) and direct assignment triggering (assign issue to bot). Labels trigger triage/analysis comments; assignment triggers actual upgrade work.
**Why:** Workshop authors need control over when the app starts consuming resources and making changes. Label-first allows review of the app's analysis before committing. Assignment-first enables confident authors to skip triage.

### 2026-02-13: Copilot SDK for Content Transformation
**By:** Kamala
**What:** Use GitHub Copilot SDK for .NET with custom SKILL.md prompts rather than raw LLM API calls.
**Why:** The SDK provides a production-grade agent loop with file operations, response streaming, and tool orchestration. Custom skills teach Copilot workshop-specific transformation patterns. This avoids reinventing the agent infrastructure.

### 2026-02-13: Convention-Based Workshop Detection with Optional Manifest
**By:** Kamala
**What:** Auto-detect workshop structure from common directory conventions (`/src/`, `/docs/`, `*.csproj`, etc.) with optional `.workshop.yml` manifest for explicit configuration.
**Why:** Most workshops follow predictable patterns; requiring a manifest creates friction. But complex workshops or edge cases benefit from explicit configuration. Hybrid approach serves both.

### 2026-02-13: Multi-Commit PR Strategy
**By:** Kamala
**What:** Generate PRs with multiple logical commits (project files → code → docs → config) rather than a single monolithic commit.
**Why:** Easier code review — reviewers can focus on one category at a time. Enables partial cherry-picking if some changes are good but others need rework. Git history tells a clearer story.

### 2026-02-13: Webhook Handler with Octokit.Webhooks.AspNetCore
**By:** Kamala
**What:** Use `Octokit.Webhooks.AspNetCore` library for webhook handling rather than manual payload parsing and signature validation.
**Why:** The library handles signature validation, payload deserialization, and event routing correctly. Security-sensitive code should use well-tested libraries. Reduces maintenance burden and CVE exposure.

### 2026-02-13: Content Type Boundaries (v1 Scope)
**By:** Kamala
**What:** v1 handles: C# code, project files, Markdown, JSON/YAML config, shell scripts. v1 does NOT handle: binary files, multimedia, Jupyter notebooks, complex migrations.
**Why:** Tight scope for v1 ensures we ship something useful quickly. Binary/multimedia files have no viable upgrade path. Notebooks have complex structure requiring separate investment. Scope can expand in v2.
