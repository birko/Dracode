---
id: TASK-123
parent: null
feature: null
status: todo
priority: P3
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Clean-up from the KoboldLair review: dead code, `null!` singletons, in-memory aggregation

## Context

Small findings from the 2026-10-08 review, none urgent alone:

- `Server/Services/FailureRecoveryService.cs` (259) is dead — `FailureRecoveryJob` replaced it and it is never registered
  (a comment in `Drake.cs` ~L2837 still names it)
- `Data/Entities/CircuitBreakerEntity.cs` is dead — nothing implements `ICircuitBreakerStore` (persistence is [[TASK-128]])
- `Program.cs` registers seven singletons as `null!` when the backend is not SQLite (L272, 332, 349, 364, 379, 386, 556);
  a consumer that resolves one gets a `NullReferenceException` far from the cause
- the SQL repositories aggregate with in-memory `GroupBy` / `Sum` where Birko's `AggregateAsync` / `CountAsync` exist
- `Program.cs` ~L452 uses `alert.TaskId?[..8]`, which throws on ids shorter than 8 characters; `LogFormatHelper.ShortId` exists

## Acceptance criteria

- [ ] The two dead files are removed, with the stale comment
- [ ] No `null!` registrations: either a null-object / in-memory implementation, or the dependents are not registered
- [ ] SQL repository totals go through `AggregateAsync` / `CountAsync`
- [ ] `ShortId` used for the alert log

## Human test plan

N/A.

## Implementation plan
