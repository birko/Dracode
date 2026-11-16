---
id: TASK-167
parent: STORY-024
feature: null
status: todo
priority: P3
assignee: agent
created: 2026-11-16
depends-on: [TASK-019]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Index the pending_decisions table and stop it growing without bound

## Context

Found at TASK-019's close review (2026-11-16). TASK-019 added the `pending_decisions` table
(`DraCode.KoboldLair/Data/Repositories/Sql/SqlPendingDecisionRepository.cs`). Two costs grow with it:

- **Resolved rows are never removed.** Every answered or timed-out decision stays forever, and the question and
  escalation payloads (`QuestionJson`, `EscalationJson`) have no length limit.
- **The table has no index.** Every Drake reads its project's unresolved decisions on every reload (each
  `DrakeExecutionService` cycle, 30s), and every Drake's timeout sweep (`DrakeMonitoringService`, 60s) reads the
  expired decisions of all projects. Both are full scans.

Harmless at today's volume (nothing parks a task in production before TASK-020), so this is housekeeping, not a
defect.

## Acceptance criteria

- [ ] Queries on unresolved decisions by project, by task, and by timeout use an index (check how Birko.Data.SQL declares one; if it cannot, say so here and choose the nearest option)
- [ ] Resolved decisions older than a retention period (configurable, with a default) are removed, or the reason to keep them is written down
- [ ] Payloads over a stated size are refused or truncated when a decision is created
- [ ] Tests cover the retention sweep and the size limit

## Out of scope

- The parking and timeout behaviour itself (TASK-019)

## Human test plan

N/A — storage housekeeping, fully asserted by repository-level tests.

## Implementation plan

_Populated by `/tasks plan TASK-167` — leave empty until then._
