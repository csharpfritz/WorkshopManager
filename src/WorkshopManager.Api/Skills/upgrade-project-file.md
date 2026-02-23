---
name: "upgrade-project-file"
description: "Upgrade project and configuration files across technology versions"
domain: "config-transformation"
---

## Context

You are upgrading a project or configuration file from **{{technology}} {{fromVersion}}** to **{{technology}} {{toVersion}}**.

This file controls build settings, dependencies, or runtime configuration for a workshop project. Changes here must be precise — a wrong version number or missing package breaks the entire build.

Release notes for the target version: {{releaseNotesUrl}}

## Instructions

### .NET Project Files (*.csproj, *.fsproj, Directory.Build.props)
- Update `<TargetFramework>` to the correct TFM for {{toVersion}} (e.g., `net8.0` → `net9.0`).
- Update NuGet package versions to their latest compatible releases for {{toVersion}}.
- Update `<LangVersion>` if the new framework default has changed.
- Remove packages that are now included in the framework (e.g., packages absorbed into the shared framework).
- Add packages that were extracted from the framework, if referenced in code.

### Node.js (package.json)
- Update `engines.node` version constraint if present.
- Update dependency versions to latest compatible releases for {{toVersion}}.
- Update `scripts` entries if CLI tool names or flags changed.

### Python (requirements.txt, pyproject.toml, setup.cfg)
- Update pinned versions for the upgraded technology.
- Update `python_requires` if present.

### Docker (Dockerfile)
- Update base image tags (e.g., `mcr.microsoft.com/dotnet/sdk:8.0` → `mcr.microsoft.com/dotnet/sdk:9.0`).
- Update any `RUN` commands that reference version-specific behavior.
- Preserve multi-stage build structure.

### General Config (appsettings.json, .env, YAML)
- Update version-pinned values.
- Update configuration keys that were renamed or restructured in {{toVersion}}.

## Constraints

- **Do NOT add dependencies** not present in the original file.
- **Do NOT remove dependencies** unless they are confirmed absorbed into the framework in {{toVersion}}.
- **Do NOT change formatting** (indentation style, spacing, comment style) beyond what the upgrade requires.
- When uncertain about the exact version number for a dependency, use a `<!-- REVIEW: verify version -->` comment.

## Output Format

Return the complete updated file. Do not return a diff.
If any version numbers are uncertain, mark them:

For XML-based files:
```xml
<!-- REVIEW: verify this package version for {{toVersion}} compatibility -->
```

For JSON-based files:
```json
"_REVIEW_": "verify version for {{toVersion}} compatibility"
```
