---
name: "analyze-breaking-changes"
description: "Analyze code against release notes to identify breaking changes and categorize remediation"
domain: "change-analysis"
---

## Context

You are analyzing workshop content to identify breaking changes when upgrading from **{{technology}} {{fromVersion}}** to **{{technology}} {{toVersion}}**.

Release notes: {{releaseNotesUrl}}

Your job is to compare the provided code or content against the release notes and produce a structured change list that tells the upgrade pipeline exactly what needs to change and whether it can be done automatically.

## Instructions

1. **Parse the release notes** at the URL provided (or from the content supplied) to identify:
   - Removed APIs
   - Deprecated APIs with replacement guidance
   - Changed default behaviors
   - New required configuration
   - Renamed namespaces, classes, or methods

2. **Scan the provided content** for references to any of the above.

3. **Categorize each finding** as:
   - `auto` — Can be fixed programmatically with high confidence (e.g., simple rename, version bump).
   - `review` — Requires human review (e.g., behavioral change, multiple migration paths, ambiguous replacement).

4. **Assess impact level** for each finding:
   - `breaking` — Code will not compile or run without this change.
   - `deprecation` — Code works but uses deprecated API; should be updated.
   - `enhancement` — New recommended pattern available; optional to adopt.

## Constraints

- Only report findings that are relevant to the provided content. Do not list all changes from the release notes.
- Do not fabricate breaking changes. If you are uncertain whether something is affected, categorize it as `review`.
- Do not suggest changes beyond what the release notes document.

## Output Format

Return valid JSON with this structure:

```json
{
  "technology": "{{technology}}",
  "fromVersion": "{{fromVersion}}",
  "toVersion": "{{toVersion}}",
  "summary": "Brief human-readable summary of the analysis",
  "totalChanges": 0,
  "changes": [
    {
      "id": "change-001",
      "category": "auto | review",
      "impact": "breaking | deprecation | enhancement",
      "description": "What needs to change and why",
      "location": "File path or code pattern affected",
      "currentCode": "The code or pattern as it exists now",
      "suggestedFix": "The recommended replacement (null if category is review)",
      "releaseNoteRef": "Section or URL anchor from release notes"
    }
  ]
}
```

Return ONLY the JSON object. Do not wrap it in markdown code fences or add commentary.
