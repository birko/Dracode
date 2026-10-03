---
id: TASK-082
parent: null
feature: null
status: todo
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

## Acceptance criteria

- [ ] Reproduce on a DB created before the column existed; find how the schema is created/updated for `usage_records`
- [ ] Existing databases gain the missing column(s) automatically on startup (no data loss), and a fresh DB still works
- [ ] A test covers an old-shape table being upgraded and a usage record then persisting
- [ ] After the fix, a live LLM call leaves a row in `usage_records` and `view_cost_report` shows it

## Out of scope

- Other tables with the same drift — check them while here; if any exist, spawn rather than widen

## Human test plan

N/A — schema upgrade and persistence are asserted by tests; the live check is the last acceptance criterion.

## Implementation plan

_Populated by `/tasks plan TASK-082` — leave empty until then._