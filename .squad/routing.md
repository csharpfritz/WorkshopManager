# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture & design | Kamala | System design, API contracts, component boundaries, scope decisions |
| GitHub App UI, webhooks, frontend surfaces | America | Webhook handlers, app installation flow, user-facing pages |
| Backend APIs, Copilot integration, content analysis | Riri | Content parsing, Copilot API calls, PR generation, diff logic |
| Code review | Kamala | Review PRs, check quality, suggest improvements |
| Testing | Kate | Write tests, find edge cases, verify fixes, CI setup |
| Scope & priorities | Kamala | What to build next, trade-offs, decisions |
| Async issue work (bugs, tests, small features) | @copilot 🤖 | Well-defined tasks matching capability profile |
| Session logging | Scribe | Automatic — never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, evaluate @copilot fit, assign `squad:{member}` label | Kamala |
| `squad:{name}` | Pick up issue and complete the work | Named member |
| `squad:copilot` | Assign to @copilot for autonomous work (if enabled) | @copilot 🤖 |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, **Kamala** triages it — analyzing content, evaluating @copilot's capability profile, assigning the right `squad:{member}` label, and commenting with triage notes.
2. **@copilot evaluation:** Kamala checks if the issue matches @copilot's capability profile (🟢 good fit / 🟡 needs review / 🔴 not suitable). If it's a good fit, Kamala may route to `squad:copilot` instead of a squad member.
3. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
4. When `squad:copilot` is applied and auto-assign is enabled, `@copilot` is assigned on the issue and picks it up autonomously.
5. Members can reassign by removing their label and adding another member's label.
6. The `squad` label is the "inbox" — untriaged issues waiting for Kamala's review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. Kamala handles all `squad` (base label) triage.
8. **@copilot routing** — when evaluating issues, check @copilot's capability profile in `team.md`. Route 🟢 good-fit tasks to `squad:copilot`. Flag 🟡 needs-review tasks for PR review. Keep 🔴 not-suitable tasks with squad members.
