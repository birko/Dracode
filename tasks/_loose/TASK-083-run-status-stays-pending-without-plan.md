---
id: TASK-083
parent: null
feature: null
status: todo
priority: P3
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: [FIELD-002]
pr: null
github-issue: null
jira-key: null
---

# A run without a plan reports "pending" until it finishes — it never shows "running"

## Context

Found 2026-10-03 during TASK-044's live sign-off. `RunRegistry` moves a run to `Running` only on `PlanStepUpdatedEvent`
(and one other event), so an ad-hoc run — which has no plan — reads `pending` on `GET /api/v1/runs/{id}` for its whole life
(observed: 25 s of `pending` then `completed`, `completedSteps: 0`). A poller cannot tell "queued" from "working".

## Acceptance criteria

- [ ] A run moves to `running` as soon as its Kobold starts (first started/iteration event), with or without a plan
- [ ] `RunRegistryTests` covers a plan-less run going pending → running → completed

## Out of scope

- SSE streaming (TASK-045)

## Human test plan

N/A — the state transition is asserted by RunRegistryTests.

## Implementation plan

_Populated by `/tasks plan TASK-083` — leave empty until then._