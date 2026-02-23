---
name: "upgrade-configuration"
description: "Upgrade configuration files across technology versions"
domain: "config-transformation"
---

## Context

You are upgrading a configuration file from **{{technology}} {{fromVersion}}** to **{{technology}} {{toVersion}}**.

This file controls runtime behavior, environment settings, or deployment configuration for a workshop project. Changes must be precise — incorrect values can cause runtime failures that are harder to diagnose than build errors.

Release notes for the target version: {{releaseNotesUrl}}

## Instructions

### Application Settings (appsettings.json, .env)
- Update version-pinned values that reference {{fromVersion}} to {{toVersion}}.
- Update configuration keys that were renamed or restructured in {{toVersion}}.
- Preserve comments and formatting structure.

### Docker (Dockerfile, docker-compose.yml)
- Update base image tags (e.g., `mcr.microsoft.com/dotnet/sdk:8.0` → `mcr.microsoft.com/dotnet/sdk:9.0`).
- Update any `RUN` commands that reference version-specific behavior.
- Preserve multi-stage build structure.
- Update compose service image versions if they reference the upgraded technology.

### DevContainer (.devcontainer/devcontainer.json)
- Update the base image or feature versions to match {{toVersion}}.
- Update any extensions or settings that are version-specific.

### CI/CD (.github/workflows/*.yml, azure-pipelines.yml)
- Update action versions and SDK setup steps (e.g., `setup-dotnet@v4` with `dotnet-version: '9.0.x'`).
- Update any version-specific build or test commands.

### General YAML/JSON Config
- Update version-pinned values.
- Preserve file structure, comments, and formatting.

## Constraints

- **Do NOT add configuration** not present in the original file.
- **Do NOT remove configuration** unless it is confirmed obsolete in {{toVersion}}.
- **Do NOT change formatting** (indentation style, spacing, comment style) beyond what the upgrade requires.
- When uncertain about the exact value, mark it for review.

## Output Format

Return the complete updated file. Do not return a diff.
If any values are uncertain, mark them:

For JSON-based files:
```json
"_REVIEW_": "verify value for {{toVersion}} compatibility"
```

For YAML-based files:
```yaml
# REVIEW: verify value for {{toVersion}} compatibility
```

For Dockerfile:
```dockerfile
# REVIEW: verify base image tag for {{toVersion}}
```
