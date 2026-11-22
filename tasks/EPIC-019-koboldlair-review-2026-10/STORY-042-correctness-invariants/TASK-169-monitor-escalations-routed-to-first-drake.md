---
id: TASK-169
parent: STORY-042
feature: null
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: []
blocks: []
findings: [FIELD-023]
pr: 39
github-issue: null
jira-key: null
---

# Reasoning-monitor escalations go to the project's first Drake, not the one holding the task

## Context

Found at TASK-020's close review (2026-11-16). `ReasoningMonitorService.RouteEscalationWithRetryAsync`
(`DraCode.KoboldLair.Server/Services/ReasoningMonitorService.cs` ~223–233) routes an escalation to
`_drakeFactory.GetDrakesByProject(projectId).FirstOrDefault()`. A project has one Drake per task area, so in a
project with more than one area the escalation often reaches a Drake whose tracker does not hold the task:

- the automatic routes look the task up in that Drake's tracker, so `NeedsSplit` / `TaskInfeasible` /
  `MissingDependency` / `WrongAgentType` report "Task not found" and do nothing;
- the human route TASK-020 added returns early for a task the Drake does not hold, so once TASK-023's policy chooses
  `HumanDecisionRequired`, monitor-raised escalations in multi-area projects silently fall back to that no-op.

Kobold-raised escalations are unaffected: each Kobold's escalation goes to the Drake that summoned it.

## Acceptance criteria

- [x] A monitor escalation is handled by the Drake whose tracker holds its task; when none holds it, that is logged and nothing is routed blindly
- [x] A test with two Drakes of one project, the task in the second, shows the second Drake handles it; red on the current code

## Out of scope

- The escalation routes themselves

## Human test plan

N/A — routing between in-memory Drakes, fully asserted by the test above.

## Implementation plan

1. `Drake.HoldsTask(taskId)`: whether this Drake's tracker holds the task.
2. `ReasoningMonitorService.RouteToHoldingDrakeAsync` (static, `internal`): route to the Drake that holds the alert's
   task; retry while none does; then log and route nothing. `RouteEscalationWithRetryAsync` calls it.
3. `MonitorEscalationRoutingTests` with two real Drakes of one project.

## Notes

- **Fix:** `ReasoningMonitorService.RouteToHoldingDrakeAsync` picks the Drake whose tracker holds the alert's task
  (`Drake.HoldsTask`), never the project's first Drake. While none holds it (no Drake yet, or the task not loaded yet)
  it looks again after the retry delay (30 s, three attempts, as before); after the last attempt it logs a warning
  naming the task and the number of Drakes, and routes nothing. An alert without a task id is logged and not routed.
- **Tests:** `MonitorEscalationRoutingTests` (2, both red with the old first-Drake choice): a WrongAgentType escalation
  for a task in the second of two Drakes is handled by that Drake (the task goes back to Unassigned); a task no Drake
  holds is routed nowhere, after looking again, and the other Drakes' tasks are untouched. Suites .NET 560/560, e2e
  137/137. Human test N/A, as the task says.
- **Seen while testing:** `auth.spec.ts` › "the user name from the token is shown as text" fails about 1 run in 5–8
  (its `/api/config` route is still fetching when the page closes) → TASK-189.
