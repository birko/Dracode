---
id: TASK-082
parent: null
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: [FIELD-001]
pr: null
github-issue: null
jira-key: null
---

# Usage records are never saved: the usage_records table has no EstimatedCostUsd column

## Context

Found 2026-10-03 while signing off TASK-044/073 against the local dev server (SQLite DB under `C:/Source/DraCode-Projects`). Every LLM call logs
`Failed to persist usage record for Z.AI (...)` with `SqliteException: table usage_records has no column named EstimatedCostUsd`
(`SqlUsageRepository.RecordUsageAsync`, warning swallowed). So the cost report, daily/monthly/project budgets and their enforcement all
run on an empty table — budget limits can never trigger. The entity gained the column but the existing table was never migrated; a fresh DB
is probably fine, an older one is not.

**Cause, corrected at planning (2026-10-03):** the entity did *not* gain the column later — `EstimatedCostUsd` has been on it
since it was created (d9bcf0e, 2026-03-19). Birko's field mapping had no case for `double` then, so `CREATE TABLE` silently left
the column out; Birko 34928514 (2026-08-08, SH-H037) added the mapping, and from then on every INSERT named a column the table
lacked. "Created before the column existed" in criterion 1 therefore means "created before Birko 34928514". The dev DB had 6 rows,
all from before August.

## Acceptance criteria

- [x] Reproduce on a DB created before the column existed; find how the schema is created/updated for `usage_records` — dev DB `.schema usage_records` lacks the column; the table comes from `CreateSchemaAsync`, which is `CREATE TABLE IF NOT EXISTS` only
- [x] Existing databases gain the missing column(s) automatically on startup (no data loss), and a fresh DB still works — `SqlUsageRepository.InitializeAsync` adds every `Missing` column Birko's `DetectDrift` reports, nullable with a type default
- [x] A test covers an old-shape table being upgraded and a usage record then persisting — `SqlUsageRepositoryTests` (3 tests; the two old-shape ones failed before the fix, so they can fail); full suite 175/175
- [ ] After the fix, a live LLM call leaves a row in `usage_records` and `view_cost_report` shows it — ⚠ NOT MET (second half) — split to TASK-086. First half met: live on the dev DB the column was added at startup, an ad-hoc run took `usage_records` 6 → 9 rows and no persist warning was logged. `view_cost_report` is a Dragon tool and Dragon cannot run until TASK-086; that half is now TASK-086's acceptance criterion "Carried from TASK-082". (Rows show cost 0.0 because `CostTracking.Pricing` is empty in appsettings — configuration, not this bug.)

## Out of scope

- Other tables with the same drift — check them while here; if any exist, spawn rather than widen. Checked at planning: only `UsageRecordEntity` (`double`) and `DomainEventEntity.Version` (`long`) use the formerly unmapped types, and the dev DB's `domain_events` already has `Version`. Nothing to spawn.

## Human test plan

N/A — schema upgrade and persistence are asserted by tests; the live check is the last acceptance criterion.

## Implementation plan

1. Reproduce read-only on the dev DB (`.schema usage_records`) and trace the table to `CreateSchemaAsync` (`CREATE TABLE IF NOT EXISTS`, never alters).
2. Red test first: `DraCode.KoboldLair.Tests/Data/SqlUsageRepositoryTests.cs` builds the exact old DDL plus one row, initializes the repository, records a usage with cost, and reads it back (the write swallows exceptions, so assert the read side). Also: re-initializing is a no-op; a fresh DB works.
3. Fix in `SqlUsageRepository.InitializeAsync`: after `CreateSchemaAsync`, run Birko's `Connector.DetectDrift(typeof(KoboldLairUsageRecord))` (DraCode's `UsageRecordEntity`, aliased because Birko has one of the same name) and `ALTER TABLE … ADD COLUMN <declared type> DEFAULT 0|''` for each `Missing` column. Not Birko's `AlterTableAddAsync`: it writes `NOT NULL` with no `DEFAULT`, which SQLite refuses on a table with rows.
4. Live check on the dev server: the column-added log line, rows persisting after an LLM call, no persist warning.

Tradeoff: fixed locally. The general gap (schema-ensure never adds columns; NOT NULL `ALTER ADD` lacks a default) belongs in the Birko framework's own task tree — filed as Birko TASK-510; once it ships, this helper becomes a call to it.