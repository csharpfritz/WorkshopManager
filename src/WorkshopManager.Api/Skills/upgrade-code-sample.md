---
name: "upgrade-code-sample"
description: "Upgrade a code sample from one technology version to another"
domain: "code-transformation"
---

## Context

You are upgrading a code sample in a software development workshop from **{{technology}} {{fromVersion}}** to **{{technology}} {{toVersion}}**.

Workshop code samples exist to teach learners specific concepts. The code is pedagogical — correctness matters, but so does clarity and teachability.

Release notes for the target version: {{releaseNotesUrl}}

## Instructions

1. **Update API calls** — Replace any APIs deprecated or removed in {{toVersion}} with their recommended replacements.
2. **Fix deprecations** — If an API still works but is deprecated in {{toVersion}}, update it to the recommended alternative and note the change in a comment.
3. **Preserve pedagogical intent** — The code exists to teach a concept. Do not restructure the code in ways that obscure the lesson. If the original used a verbose pattern to illustrate a point, keep it verbose unless the new version fundamentally changes the approach.
4. **Maintain coding style** — Match the existing formatting, naming conventions, and structural patterns. Do not reformat code that isn't affected by the upgrade.
5. **Keep explanatory comments** — Comments that explain "why" something is done a certain way are critical for learners. Preserve them. Update them only if the "why" has changed due to the version upgrade.
6. **Update version-specific references** — String literals, configuration values, or constants that reference {{fromVersion}} should be updated to {{toVersion}}.

## Constraints

- **Do NOT add features** not present in the original code, even if {{toVersion}} introduces them.
- **Do NOT refactor** for "modern best practices" unless the original pattern is broken in {{toVersion}}.
- **Do NOT remove comments** unless they reference behavior that no longer exists and would confuse learners.
- **Do NOT change variable names, method names, or class names** unless required by the API migration.
- If a change is ambiguous or has multiple valid migration paths, choose the path closest to the original intent and add a `// NOTE:` comment explaining the choice.

## Output Format

Return the complete updated file content. Do not return a diff or partial snippet.
If any changes require human review (e.g., behavioral change, multiple migration paths), prepend a comment block at the top of the file:

```
// REVIEW: [description of what needs human attention]
```
