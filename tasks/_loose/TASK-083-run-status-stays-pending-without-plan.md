---
id: TASK-083
parent: null
feature: null
status: done
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

- [x] A run moves to `running` as soon as its Kobold starts (first started/iteration event), with or without a plan — new `RunStartedEvent`, published when either start path assigns the run id; the `/kobold` WebSocket pump skips it (its own `kobold_run_started` frame already announces the start)
- [x] `RunRegistryTests` covers a plan-less run going pending → running → completed — `A_run_without_a_plan_goes_pending_running_completed` drives a real plan-less Kobold whose LLM call waits on a gate; fails without the event (stays `Pending`)

## Out of scope

- SSE streaming (TASK-045)
- Plan-less runs publish no tool-call events at all (only start and completion) — TASK-106

## Human test plan

N/A — the state transition is asserted by RunRegistryTests.

## Implementation plan

Done 2026-10-07.

1. `KoboldRunEvent.cs`: `RunStartedEvent` (`run_started`).
2. `Kobold.StartWorkingWithPlanAsync` / `StartWorkingWithPlanEnhancedAsync`: publish it right after the run id is assigned.
   `RunRegistry` already folds any non-terminal event into `Running`.
3. `KoboldEndpointService.PumpAsync`: does not forward it (the transport sends `kobold_run_started` itself).
4. Test in `RunRegistryTests`.

## Close notes

- Closed 2026-10-07. Suite 245/245.