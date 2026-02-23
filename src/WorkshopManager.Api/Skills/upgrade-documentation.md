---
name: "upgrade-documentation"
description: "Upgrade workshop documentation and instructions across technology versions"
domain: "documentation-transformation"
---

## Context

You are upgrading workshop documentation (Markdown) from **{{technology}} {{fromVersion}}** to **{{technology}} {{toVersion}}**.

Workshop documentation guides learners through exercises step by step. It references specific commands, APIs, UI paths, and code patterns. All of these must stay accurate for the target version.

Release notes for the target version: {{releaseNotesUrl}}

## Instructions

1. **Update version references** — Replace all mentions of {{fromVersion}} with {{toVersion}} where they refer to the technology being upgraded.
2. **Update CLI commands** — If command syntax, flags, or tool names changed between versions, update them. Example: `dotnet new webapi` → `dotnet new webapi --use-controllers`.
3. **Update API and class references** — If the documentation references specific classes, methods, or namespaces that changed, update them to match {{toVersion}}.
4. **Update configuration examples** — Inline code blocks showing configuration (JSON, YAML, XML) must reflect the new version's format and defaults.
5. **Preserve teaching flow** — Do not reorganize sections, reorder steps, or change the learning progression. The pedagogical sequence was designed intentionally.
6. **Preserve learning objectives** — If the document states learning objectives, ensure they remain accurate. If an objective references a deprecated concept, flag it for human review.
7. **Flag non-textual content** — If the document references screenshots, architecture diagrams, or embedded images, add a review marker since these cannot be updated automatically.

## Constraints

- **Do NOT add new sections** or teaching content not present in the original.
- **Do NOT remove sections** even if they seem outdated — flag them for review instead.
- **Do NOT change the document structure** (heading levels, section ordering).
- **Do NOT update version numbers** that refer to other technologies (e.g., if upgrading .NET but the doc also mentions Node.js 18, leave Node.js alone).

## Review Markers

Insert the following markers where human review is needed:

```markdown
<!-- REVIEW: [description] -->
```

Use review markers for:
- Screenshots or images that may show outdated UI
- Architecture diagrams that may have changed
- Sections referencing behavior you are uncertain about in {{toVersion}}
- Learning objectives that may need rewording

## Output Format

Return the complete updated document. Do not return a diff.
