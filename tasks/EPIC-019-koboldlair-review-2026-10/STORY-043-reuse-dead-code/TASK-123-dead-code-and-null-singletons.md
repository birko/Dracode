---
id: TASK-123
parent: STORY-043
feature: null
status: done
priority: P3
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: 67
github-issue: null
jira-key: null
findings: [CR-3]
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

- [x] The two dead files are removed, with the stale comment
- [ ] No `null!` registrations: either a null-object / in-memory implementation, or the dependents are not registered
      — ⚠ MOVED to TASK-200 (2026-11-25, the owner's decision): production is to run on PostgreSQL, so the stores
      must work on any database rather than be SQLite-or-nothing; that removes the `null!`s
- [x] SQL repository totals go through `AggregateAsync` / `CountAsync` — the usage totals were the only in-memory ones
      left; the other repositories already count with `CountAsync`
- [x] `ShortId` used for the alert log

## Human test plan

N/A.

## Implementation plan

Picked 2026-11-25. Each finding was re-checked first: both dead files were still there, `Program.cs` now had ten `null!`
registrations, and the `[..8]` was at line 539.

1. The `null!` item moved to TASK-200 (the owner: production is to run on PostgreSQL, so the stores must work on any
   database, which removes them; deciding "SQLite or nothing" earlier would not).
2. `SqlUsageRepositoryTests` pins what each usage total returns (by provider, by project, total spend with and without
   a provider, spend by project, a project's spend) on a seeded SQLite file. The tests pass on the old code too: this is
   a refactor, and they hold it to the same answers.
3. `SqlUsageRepository` computes the five totals with the store's `AggregateAsync` (SQL `GROUP BY`; count, summed
   tokens, summed cost) instead of reading every record into memory. The repository does not expose its store, so the
   class keeps the `AsyncSQLiteStore` it builds the repository on.
4. Removed `FailureRecoveryService.cs` and `CircuitBreakerEntity.cs`; Drake's comment names `FailureRecoveryJob`; the
   escalation notification uses `LogFormatHelper.ShortId`; CLAUDE.md no longer says a `CircuitBreakerEntity` table
   rehydrates the breaker (its persistence is TASK-128).

## Notes

- `AggregateAsync` is provider-neutral in Birko.Data, so the totals carry over to PostgreSQL unchanged (TASK-200).
- `ProjectStatsService` still builds the Stats tab's breakdowns (by day, by model) from a project's records in memory;
  that is a service's report, not a repository total, and was left as it is.
- .NET suite 653 passed. No client change. No human test (the plan says N/A).
