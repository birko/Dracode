---
id: TASK-093
parent: null
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: [FIELD-010]
pr: null
github-issue: null
jira-key: null
---

# A deleted project's Wyvern stays registered by name and is reused by a new project with that name

## Context

Found 2026-10-03 during TASK-091's live check. `WyvernFactory` keeps its Wyverns keyed by **project name**, and deleting
a project does not remove its Wyvern. A new project created with the same name then got the old Wyvern: the log read
"Wyvern not found for project … re-assigning" → "already has status WyrmAssigned, skipping Wyvern assignment" →
"Assigned wyvern", and the tasks it wrote carried the **deleted** project's id (`ProjectId` set at creation, TASK-091).
In one such run the analysis also came back with **0 areas / 0 tasks** and the project was marked Analyzed anyway —
whether that was the stale Wyvern or model variance is not established; a zero-area analysis being accepted as success
is worth checking on its own.

## Acceptance criteria

- [ ] Deleting a project (REST and Dragon) removes its Wyvern (and any Drakes) from the factories
- [ ] A new project with a deleted project's name gets a fresh Wyvern carrying its own project id — covered by a test
- [ ] Decide what an analysis with zero areas/tasks should do (fail and retry, or mark Failed with a reason) instead of reaching Analyzed; implement and test it

## Out of scope

- Deleting a project's task rows and files (TASK-074)

## Human test plan

N/A — factory state and the zero-area rule are unit-testable.

## Implementation plan

_Populated by `/tasks plan TASK-093` — leave empty until then._