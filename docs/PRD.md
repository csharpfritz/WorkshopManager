# WorkshopManager Product Requirements Document

> **Status:** Draft v1.1  
> **Author:** Kamala (Lead)  
> **Date:** 2026-02-13 (updated 2026-02-14)  
> **Owner:** Jeffrey T. Fritz (@csharpfritz)

---

## 1. Overview

### What It Is
WorkshopManager is a GitHub App that automates technology upgrades for software development training workshops. When a repository owner files an issue requesting a technology upgrade (e.g., "Upgrade from .NET 8 to .NET 9"), the app:

1. Analyzes the workshop content — code samples, project files, and prose documentation
2. Uses the GitHub Copilot SDK to identify and plan required changes
3. Applies updates to both code and instructional content
4. Delivers a Pull Request with all changes, ready for review

### Who It's For
- **Workshop authors** who maintain training materials across multiple technology versions
- **Developer advocates** who need to keep demos and samples current
- **Training teams** managing large catalogs of educational content

### Value Proposition
Manual upgrades of workshop content are tedious and error-prone. Authors must update code samples, verify they compile, and then update prose instructions to match. WorkshopManager automates this entire workflow, reducing hours of manual work to a single issue and PR review.

---

## 2. User Stories

### Primary Stories

| ID | As a... | I want to... | So that... |
|----|---------|--------------|------------|
| US-1 | Workshop author | File an issue requesting a technology upgrade | The app handles the tedious update work for me |
| US-2 | Workshop author | Review a PR with all proposed changes | I can verify correctness before merging |
| US-3 | Workshop author | See what was updated and why in the PR description | I understand what changed without reading every file |
| US-4 | Workshop author | Configure which content types the app should update | The app respects my project structure |

### Secondary Stories

| ID | As a... | I want to... | So that... |
|----|---------|--------------|------------|
| US-5 | Workshop author | Receive a comment if the app can't process something | I know what to handle manually |
| US-6 | Workshop author | Have the app detect my workshop structure automatically | I don't have to configure everything manually |
| US-7 | Repo maintainer | Assign issues directly to the app | The app starts working immediately |
| US-8 | Repo maintainer | Use a label to triage before assignment | I can batch-review upgrade requests before triggering work |
| US-9 | Workshop author | Provide a link to release notes in an issue | The app parses it to discover what changed and updates content accordingly |
| US-10 | Workshop author | Have Dependabot dependency updates trigger workshop content updates | Code and prose stay in sync when dependencies are bumped |

---

## 3. Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        WorkshopManager GitHub App                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌──────────────────┐    ┌─────────────────────┐   ┌──────────────────┐│
│  │  Webhook Handler │───▶│  Trigger Classifier │──▶│  Work Scheduler  ││
│  │  (ASP.NET Core)  │    │ (Issue/PR/ReleaseNote)  │                  ││
│  └──────────────────┘    └─────────────────────┘   └──────────────────┘│
│           │                       │                         │          │
│           ▼                       ▼                         ▼          │
│  ┌──────────────────┐   ┌─────────────────┐   ┌──────────────────────┐ │
│  │ GitHub API Client│   │ Release Notes   │   │   Upgrade Processor  │ │
│  │    (Octokit)     │   │  Fetcher/Parser │   │                      │ │
│  └──────────────────┘   └─────────────────┘   ├──────────────────────┤ │
│           ▲                                    │ Content Analyzer     │ │
│           │             ┌─────────────────┐   │ Copilot Integrator   │ │
│           └────────────▶│ Dependabot PR    │──▶│ Change Applier       │ │
│                         │  Detector        │   │ PR Generator         │ │
│                         └─────────────────┘   └──────────────────────┘ │
│                                                          │              │
│                                                          ▼              │
│                                                ┌──────────────────────┐ │
│                                                │  Copilot SDK Client  │ │
│                                                │      (.NET SDK)      │ │
│                                                └──────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility |
|-----------|----------------|
| **Webhook Handler** | Receives GitHub webhook events, validates signatures, routes to appropriate handlers |
| **Trigger Classifier** | Determines trigger type (issue, release notes link, Dependabot PR) and routes to appropriate handler |
| **Issue Parser** | Extracts upgrade intent from issue title/body (source version, target version, scope) |
| **Release Notes Fetcher/Parser** | Fetches release notes from URL, parses for version/API changes to infer upgrade intent |
| **Dependabot PR Detector** | Detects Dependabot-opened PRs, extracts package/version information |
| **Work Scheduler** | Manages job queue, ensures idempotency, handles retries |
| **GitHub API Client** | Authenticated operations: read repo content, create branches, commit files, open PRs |
| **Upgrade Processor** | Orchestrates the full upgrade workflow |
| **Content Analyzer** | Discovers workshop structure, identifies files needing updates |
| **Copilot Integrator** | Sends content to Copilot SDK for analysis and transformation |
| **Change Applier** | Applies Copilot-generated changes to files |
| **PR Generator** | Creates branch, commits changes, opens PR with descriptive body |

### Data Flow

1. **Trigger** → Webhook received (issue labeled or assigned)
2. **Parse** → Extract upgrade intent from issue
3. **Discover** → Scan repo for workshop structure and content
4. **Analyze** → Send content to Copilot for upgrade analysis
5. **Transform** → Apply Copilot-suggested changes
6. **Validate** → Verify changes (syntax check, build test if configured)
7. **Deliver** → Create PR with changes and descriptive summary

---

## 4. Trigger & Workflow

### Trigger Mechanisms

WorkshopManager supports four trigger mechanisms:

#### 1. Label-Based Trigger (Triage Flow)
- **Label:** `workshop-upgrade`
- **Behavior:** App comments on issue with analysis summary, awaits assignment
- **Use case:** Owner wants to review before committing to work
- **Scope:** Issues only

#### 2. Assignment-Based Trigger (Direct Flow)
- **Assignee:** `workshopmanager[bot]` (the app's bot user)
- **Behavior:** App immediately begins processing the upgrade
- **Use case:** Owner is ready for the app to do the work
- **Scope:** Issues only

#### 3. Release Notes Link Trigger (Content Discovery Flow)
- **Detection:** Issue body contains a URL to release notes (e.g., https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- **Behavior:** App fetches and parses release notes to understand what changed, infers upgrade intent, proceeds with upgrade workflow
- **Use case:** Author provides a curated link to what changed in a new release; app automatically discovers impact on workshop
- **Scope:** Issues with label `workshop-upgrade` or direct assignment
- **Supported sources:** Official release notes pages (GitHub Releases, Microsoft, Python, Node, etc.)

#### 4. Dependabot Integration Trigger (Sync Flow)
- **Detection:** Dependabot opens a PR for a dependency update (detectable via `actor: dependabot[bot]` in pull_request webhook)
- **Behavior:** App creates a companion issue/PR analyzing the dependency change and proposing workshop content updates to keep pace with code changes
- **Use case:** Dependabot bumps a NuGet/npm/Python dependency; WorkshopManager ensures instructional content stays in sync
- **Scope:** Pull requests only, when Dependabot is the PR author
- **Configuration:** Enable/disable per repository

### Workflow State Machine

```
Issue Flow (Triggers 1-3):
┌─────────┐     label added      ┌───────────┐
│  Open   │─────────────────────▶│  Triaged  │
│  Issue  │                      │           │
└─────────┘                      └───────────┘
     │                                  │
     │ assigned to bot                  │ assigned to bot
     │                                  │ OR release notes URL found
     │                                  │
     ▼                                  ▼
┌──────────────────┐         ┌──────────────────────┐
│ Parse Intent     │         │ Fetch Release Notes  │
│ (title/body)     │         │ & Extract Intent     │
└──────────────────┘         └──────────────────────┘
     │                                  │
     └──────────────┬───────────────────┘
                    ▼
┌─────────────────────────────────────────────┐
│                Processing                    │
├─────────────────────────────────────────────┤
│ 1. Clone/fetch repo content                 │
│ 2. Discover workshop structure              │
│ 3. Analyze content with Copilot             │
│ 4. Apply transformations                    │
│ 5. Validate changes                         │
│ 6. Create PR                                │
└─────────────────────────────────────────────┘
     │                                  │
     ▼ (success)                        ▼ (failure)
┌─────────────┐                  ┌─────────────┐
│  PR Open    │                  │   Failed    │
│ (links to   │                  │ (comment    │
│  issue)     │                  │  explains)  │
└─────────────┘                  └─────────────┘

Dependabot Flow (Trigger 4):
┌──────────────────┐
│ Dependabot PR    │
│ Opened           │
└──────────────────┘
     │
     ▼
┌──────────────────────────────┐
│ Extract Package/Version Info │
└──────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────────────┐
│        Companion Update Processing           │
├─────────────────────────────────────────────┤
│ 1. Analyze Dependabot PR changes            │
│ 2. Discover affected workshop content       │
│ 3. Propose instructional updates            │
│ 4. Create companion PR (or comment)         │
└─────────────────────────────────────────────┘
```

### Issue Format

The app parses issue titles and bodies for upgrade intent:

**Title patterns:**
- `Upgrade to .NET 9`
- `Update from .NET 8 to .NET 9`
- `Migrate workshop to Python 3.12`

**Body (optional, enhances precision):**
```markdown
## Upgrade Request

**From:** .NET 8
**To:** .NET 9
**Scope:** all (or: code-only, docs-only, specific-modules)

### Release Notes
https://dotnet.microsoft.com/en-us/download/dotnet/9.0

### Notes
- Focus on minimal API changes
- Update NuGet packages to latest stable
```

### Release Notes Link Format

When an issue contains a link to release notes, the app extracts it from the body and parses it:

```markdown
## Upgrade Request

Found release notes for the new version:
https://dotnet.microsoft.com/en-us/download/dotnet/9.0

The app will analyze the release notes to understand what changed and propose updates.
```

Supported release notes sources:
- **Microsoft .NET:** https://dotnet.microsoft.com/en-us/download/dotnet/X.X
- **GitHub Releases:** Any repository with standard GitHub release format
- **Python:** https://docs.python.org/3/whatsnew/
- **Node.js:** https://nodejs.org/en/blog/release/vX.X.X/
- **Other frameworks:** Common release note URL patterns

---

## 5. Copilot Integration

### SDK Usage

WorkshopManager uses the **GitHub Copilot SDK for .NET** to perform intelligent content analysis and transformation.

```csharp
// Conceptual usage
var client = new CopilotClient();
await client.StartAsync();

var session = await client.CreateSessionAsync(new SessionOptions
{
    Model = "claude-sonnet-4.5",
    SkillDirectories = new[] { "./skills/workshop-upgrade/SKILL.md" }
});

var result = await session.SendAndWaitAsync(new
{
    Prompt = $"""
        Analyze this C# code file and update it from .NET 8 to .NET 9.
        
        File: {filePath}
        Content:
        ```csharp
        {fileContent}
        ```
        
        Return the updated code with explanations of changes.
        """
});
```

### Copilot Skill Definition

The app ships with a custom SKILL.md that teaches Copilot how to perform workshop upgrades:

```markdown
---
name: "workshop-upgrade"
description: "Upgrade workshop content across technology versions"
domain: "code-transformation"
---

## Context
You are upgrading software workshop content from one technology version to another.
Workshops contain both executable code samples and prose instructions that reference the code.

## Patterns
- When upgrading code, preserve the pedagogical intent
- Update version numbers in project files
- Migrate deprecated APIs to recommended alternatives
- Update prose that references specific APIs or patterns
- Preserve comments that explain concepts to learners
```

### Copilot Request Types

| Request Type | Purpose | Input | Output |
|--------------|---------|-------|--------|
| **Analyze** | Identify what needs to change | File content + version info | List of required changes |
| **Transform Code** | Update code files | Code + change spec | Updated code |
| **Transform Prose** | Update documentation | Markdown + change spec | Updated markdown |
| **Summarize** | Generate PR description | All changes made | Human-readable summary |

### Rate Limiting & Costs

- Copilot SDK calls are metered; the app should batch intelligently
- Group related files into single analysis requests where possible
- Cache analysis results within a single upgrade job

---

## 6. Content Analysis

### Workshop Structure Detection

The app uses a hybrid approach to understand workshop structure:

#### 1. Convention-Based Detection (Default)
Common patterns the app recognizes:
- `/src/` or `/code/` — Code samples
- `/docs/` or `/instructions/` — Prose content
- `*.csproj`, `*.fsproj` — .NET projects
- `package.json` — Node.js projects
- `requirements.txt`, `pyproject.toml` — Python projects
- `README.md`, `WORKSHOP.md` — Workshop entry points
- `/modules/`, `/labs/`, `/exercises/` — Workshop sections

#### 2. Manifest-Based Detection (Optional)
Repos can include a `.workshop.yml` manifest:

```yaml
# .workshop.yml
name: "Intro to ASP.NET Core"
structure:
  modules:
    - path: "module-01-setup"
      code: "src/"
      docs: "instructions.md"
    - path: "module-02-routing"
      code: "src/"
      docs: "instructions.md"
  shared:
    - "shared-assets/"
technology:
  primary: "dotnet"
  version: "8.0"
```

#### 3. Auto-Detection Fallback
If no conventions match and no manifest exists:
- Scan for common project file types
- Use Copilot to analyze README and infer structure
- Comment on issue asking for clarification if ambiguous

### Content Types Handled

| Content Type | File Patterns | Analysis Strategy |
|--------------|---------------|-------------------|
| **C# Code** | `*.cs` | Copilot code transformation |
| **Project Files** | `*.csproj`, `*.sln` | XML parsing + version updates |
| **Markdown** | `*.md` | Copilot prose transformation |
| **JSON Config** | `*.json` | Schema-aware updates |
| **YAML Config** | `*.yml`, `*.yaml` | Schema-aware updates |
| **Scripts** | `*.ps1`, `*.sh` | Copilot code transformation |

### Content Types NOT Handled (v1)

- Binary files (images, compiled assets)
- Video/audio content
- Interactive notebooks (`.ipynb`) — future consideration
- External dependencies (NuGet, npm packages are updated, not analyzed)

---

## 7. PR Generation

### Branch Strategy

- **Branch name:** `workshop-upgrade/{issue-number}-{target-version}`
- **Example:** `workshop-upgrade/42-dotnet-9`
- **Base:** Default branch (typically `main`)

### Commit Strategy

The app creates **multiple logical commits** rather than one big commit:

1. `chore: update project files to {target-version}`
2. `refactor: update code samples for {target-version}`
3. `docs: update instructions for {target-version}`
4. `chore: update configuration files`

This makes review easier and allows partial cherry-picking if needed.

### PR Structure

```markdown
## 🔄 Workshop Upgrade: .NET 8 → .NET 9

This PR upgrades the workshop content from .NET 8 to .NET 9.

### Summary
- Updated 12 code files
- Updated 8 documentation files
- Updated 3 project files

### Changes by Category

#### Project Files
- `src/Module1/Module1.csproj` — Target framework updated
- `src/Module2/Module2.csproj` — Target framework updated

#### Code Updates
| File | Changes |
|------|---------|
| `src/Module1/Program.cs` | Updated `WebApplication.CreateBuilder` pattern |
| `src/Module2/Startup.cs` | Migrated to minimal hosting model |

#### Documentation Updates
| File | Changes |
|------|---------|
| `docs/module-01.md` | Updated code snippets, version references |
| `docs/module-02.md` | Updated API references |

### Validation
- ✅ All projects build successfully
- ⚠️ Manual review recommended for: `src/Module3/` (complex changes)

### Notes
- This PR was generated by WorkshopManager in response to #42
- Review carefully before merging

---
Closes #42
```

### PR Labels

The app applies labels to the PR for easy filtering:
- `workshop-upgrade`
- `automated`
- Technology-specific: `dotnet`, `python`, `node`, etc.

---

## 8. Configuration

### Repository Configuration File

Repos can customize behavior via `.github/workshop-manager.yml`:

```yaml
# .github/workshop-manager.yml

# Workshop structure (optional — auto-detected if omitted)
structure:
  code_paths:
    - "src/"
    - "samples/"
  docs_paths:
    - "docs/"
    - "instructions/"
  ignore:
    - "archived/"
    - "*.backup.*"

# Content handling
content:
  update_code: true
  update_docs: true
  update_project_files: true

# Validation
validation:
  build_before_pr: true
  build_command: "dotnet build"

# PR behavior
pull_request:
  draft: false
  reviewers:
    - "@csharpfritz"
  labels:
    - "needs-review"

# Release notes parsing
release_notes:
  enabled: true
  auto_detect_version: true
  api_analysis: true

# Dependabot integration
dependabot:
  enabled: true
  create_companion_pr: true
  auto_merge_enabled: false
  triggers:
    - ".NET"
    - "Node.js"
    - "Python"

# Copilot settings
copilot:
  model: "claude-sonnet-4.5"  # or "gpt-5"
  custom_instructions: |
    This workshop uses a specific coding style:
    - Prefer explicit types over var
    - Include XML doc comments on public methods
```

### GitHub App Settings

Configured in the GitHub App manifest:

| Setting | Value |
|---------|-------|
| **Webhook events** | `issues`, `issue_comment`, `pull_request` |
| **Permissions** | `contents: write`, `issues: write`, `pull_requests: write` |
| **User-to-server tokens** | Required for Copilot SDK access |

---

## 9. Tech Stack

### Runtime & Framework

| Component | Technology | Rationale |
|-----------|------------|-----------|
| **Runtime** | .NET 9 | Latest LTS, matches owner's expertise |
| **Web Framework** | ASP.NET Core Minimal APIs | Lightweight, modern, sufficient for webhook handling |
| **Hosting** | Azure App Service or Azure Functions | Easy deployment, scaling |

### Libraries & SDKs

| Library | Purpose |
|---------|---------|
| **Octokit.net** | GitHub API client (REST) |
| **Octokit.Webhooks.AspNetCore** | Webhook event handling |
| **Octokit.GraphQL** | GitHub GraphQL API (for efficient queries) |
| **GitHub Copilot SDK (.NET)** | AI-powered content analysis and transformation |
| **YamlDotNet** | YAML parsing for config files |
| **Markdig** | Markdown parsing for documentation analysis |

### Infrastructure

| Component | Technology |
|-----------|------------|
| **Secrets** | Azure Key Vault or GitHub App private key storage |
| **Logging** | Application Insights |
| **Queue (optional)** | Azure Storage Queues (for job durability) |

---

## 10. Non-Goals / Out of Scope

The following are explicitly **NOT** in scope for v1:

| Non-Goal | Rationale |
|----------|-----------|
| **Multi-repo upgrades** | Focus on single-repo first; batch operations add complexity |
| **Interactive/notebook support** | `.ipynb` files have complex structure; defer to future version |
| **Video/multimedia updates** | No technical path to update video content |
| **Breaking change migrations** | Focus on version bumps, not major architectural migrations |
| **Custom Copilot model training** | Use off-the-shelf models with custom prompts |
| **Web UI/Dashboard** | All interaction through GitHub issues/PRs |
| **Support for non-GitHub platforms** | GitHub-only; no GitLab, Bitbucket, etc. |
| **Real-time collaboration** | Async workflow via issues/PRs |

---

## 11. Open Questions

These items require Jeffrey's input before finalizing:

| # | Question | Options | Impact |
|---|----------|---------|--------|
| 1 | **Default trigger mechanism?** | assignment-first | UX for workshop authors |
| 2 | **Copilot model preference?** | configurable with a default of Claude Sonnet 4.5 | Cost, quality, latency tradeoffs |
| 3 | **Build validation requirement?** | Always build before PR | Time to PR, confidence level |
| 4 | **Multi-language workshop support?** | .NET-first - let's prove this works and then roll out to other tech stacks | Development scope |
| 5 | **Hosting preference?** | Container Apps | Cost, ops model |
| 6 | **Failure notification channel?** | Issue comment only - allow GitHub notifications to manage further notifications beyond that | Author notification preferences |
| 7 | **Workshop manifest format?** | Custom `.workshop.yml` | Compatibility with existing tooling |

---

## 12. Work Item Decomposition

### Phase 1: Foundation (Sprint 1-2)

| ID | Work Item | Assignee | Dependencies | Estimate |
|----|-----------|----------|--------------|----------|
| WI-01 | Set up GitHub App registration and webhook endpoint | America | — | 3 pts |
| WI-02 | Implement webhook signature validation and routing | America | WI-01 | 2 pts |
| WI-03 | Design issue parsing logic (extract upgrade intent) | Kamala | — | 2 pts |
| WI-04 | Implement issue parser service | Riri | WI-03 | 3 pts |
| WI-05 | Set up Copilot SDK integration scaffold | Riri | — | 3 pts |
| WI-06 | Write unit tests for issue parser | Kate | WI-04 | 2 pts |
| WI-07 | Write integration tests for webhook handler | Kate | WI-02 | 3 pts |

### Phase 2: Content Analysis (Sprint 2-3)

| ID | Work Item | Assignee | Dependencies | Estimate |
|----|-----------|----------|--------------|----------|
| WI-08 | Design workshop structure detection algorithm | Kamala | — | 2 pts |
| WI-09 | Implement convention-based content discovery | Riri | WI-08 | 5 pts |
| WI-10 | Implement manifest-based content discovery | Riri | WI-08 | 3 pts |
| WI-11 | Define Copilot SKILL.md for workshop upgrades | Kamala | WI-05 | 3 pts |
| WI-12 | Implement Copilot analysis service | Riri | WI-05, WI-11 | 5 pts |
| WI-13 | Write tests for content discovery | Kate | WI-09, WI-10 | 3 pts |

### Phase 3: Transformation & PR (Sprint 3-4)

| ID | Work Item | Assignee | Dependencies | Estimate |
|----|-----------|----------|--------------|----------|
| WI-14 | Design change application strategy | Kamala | WI-12 | 2 pts |
| WI-15 | Implement code transformation service | Riri | WI-12, WI-14 | 5 pts |
| WI-16 | Implement documentation transformation service | Riri | WI-12, WI-14 | 3 pts |
| WI-17 | Implement PR generation service | America | WI-15, WI-16 | 5 pts |
| WI-18 | Implement build validation (optional) | Riri | WI-15 | 3 pts |
| WI-19 | Write end-to-end tests | Kate | WI-17 | 5 pts |

### Phase 4: Polish & Configuration (Sprint 4)

| ID | Work Item | Assignee | Dependencies | Estimate |
|----|-----------|----------|--------------|----------|
| WI-20 | Implement repository configuration loading | America | — | 2 pts |
| WI-21 | Add error handling and issue commenting | America | WI-17 | 3 pts |
| WI-22 | Code review and architecture validation | Kamala | WI-17 | 3 pts |
| WI-23 | Security review | Kamala | WI-22 | 2 pts |
| WI-24 | Documentation for workshop authors | Kamala | WI-22 | 2 pts |
| WI-25 | Final QA pass | Kate | WI-22 | 3 pts |

### Phase 5: Release Notes & Dependabot Integration (Sprint 5-6)

| ID | Work Item | Assignee | Dependencies | Estimate |
|----|-----------|----------|--------------|----------|
| WI-26 | Design release notes fetcher and parser | Kamala | WI-03 | 2 pts |
| WI-27 | Implement release notes HTTP fetcher | America | WI-26 | 2 pts |
| WI-28 | Implement release notes parser (extract version/API changes) | Riri | WI-26 | 4 pts |
| WI-29 | Integrate release notes analysis into upgrade workflow | Riri | WI-28, WI-12 | 3 pts |
| WI-30 | Update trigger classifier to detect release notes URLs | America | WI-26 | 2 pts |
| WI-31 | Design Dependabot PR detection strategy | Kamala | — | 1 pt |
| WI-32 | Implement Dependabot PR detector | America | WI-31 | 2 pts |
| WI-33 | Extract package/version info from Dependabot PR | Riri | WI-32 | 2 pts |
| WI-34 | Implement companion PR generation for Dependabot updates | America | WI-33, WI-17 | 4 pts |
| WI-35 | Add release notes and Dependabot config options | America | — | 2 pts |
| WI-36 | Write integration tests for release notes trigger | Kate | WI-29 | 3 pts |
| WI-37 | Write integration tests for Dependabot trigger | Kate | WI-34 | 3 pts |
| WI-38 | Update GitHub App manifest for pull_request events | America | WI-32 | 1 pt |

### Team Allocation Summary

| Team Member | Primary Focus | Work Items |
|-------------|---------------|------------|
| **Kamala** | Architecture, design, code review | WI-03, WI-08, WI-11, WI-14, WI-22, WI-23, WI-24, WI-26, WI-31 |
| **America** | GitHub App surfaces, webhooks, PR generation | WI-01, WI-02, WI-17, WI-20, WI-21, WI-27, WI-30, WI-32, WI-34, WI-35, WI-38 |
| **Riri** | Backend services, Copilot integration, content analysis | WI-04, WI-05, WI-09, WI-10, WI-12, WI-15, WI-16, WI-18, WI-28, WI-29, WI-33 |
| **Kate** | Testing, QA, CI setup | WI-06, WI-07, WI-13, WI-19, WI-25, WI-36, WI-37 |

---

## Appendix A: Glossary

| Term | Definition |
|------|------------|
| **Workshop** | A repository containing educational materials: code samples and prose instructions |
| **Upgrade** | The process of updating workshop content from one technology version to another |
| **Module** | A logical section of a workshop (e.g., "Module 1: Getting Started") |
| **Prose** | Non-code instructional content (typically Markdown) |
| **Trigger** | An event that causes the app to start processing (label, assignment, release notes URL, or Dependabot PR) |
| **Release Notes** | Official documentation of changes in a new software version, used to infer what needs updating |
| **Dependabot** | GitHub's automated dependency update bot that suggests package version upgrades |
| **Companion PR** | A PR created by WorkshopManager alongside a Dependabot PR to keep workshop content in sync |

## Appendix B: Related Resources

- [GitHub Apps Documentation](https://docs.github.com/en/apps)
- [GitHub Copilot SDK](https://github.com/github/copilot-sdk)
- [Octokit.net](https://github.com/octokit/octokit.net)
- [Octokit.Webhooks](https://github.com/octokit/webhooks.net)

---

*Document generated by Kamala (Lead) for WorkshopManager project.*
