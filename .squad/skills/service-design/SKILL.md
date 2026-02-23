# Skill: Service Design Pattern

> Reusable pattern for designing new service layers in WorkshopManager.

## When to Use
- Designing a new service interface for the WorkshopManager pipeline
- Adding a new phase to the upgrade workflow

## Pattern

### 1. Data Model First
Define immutable `record` types for:
- Per-item result (e.g., `TransformationResult`) — captures success/failure, content, error, metrics
- Aggregate summary (e.g., `TransformationSummary`) — with computed properties for filtering
- Final outcome (e.g., `PullRequestResult`) — what the caller needs to know

### 2. Interface Design
- Accept batch inputs, process per-item internally
- Take `CancellationToken ct = default` on all async methods
- Return result types, never throw for expected failures (per-item errors captured in results)
- Keep interfaces small: one method per concern

### 3. Separation by Prompt Strategy
When different content types need different AI/Copilot prompting:
- Separate interfaces (not one with internal branching)
- Rationale: different context needs, different failure severity, independent testability

### 4. DI Lifetime
- `Scoped` for services that share per-request state (e.g., GitHub auth tokens)
- `Singleton` for stateless utilities (e.g., `SkillResolver`, `FileClassifier`)

### 5. Error Handling
- Per-item fault isolation: catch exceptions inside loops, produce failure result
- Aggregate decides pipeline behavior: "all failed" = abort, "some failed" = partial success
- Never commit failed items; report them in output

### 6. Orchestrator
- One orchestrator service owns the full pipeline
- Webhook handler calls orchestrator; orchestrator calls services
- Orchestrator returns a typed result, never throws for business failures

## Example
See `docs/design/phase3-transformation-pr.md` for the canonical application of this pattern.
