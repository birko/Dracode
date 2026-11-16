---
id: TASK-169
parent: STORY-042
feature: null
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: []
blocks: []
findings: [FIELD-023]
pr: null
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

- [ ] A monitor escalation is handled by the Drake whose tracker holds its task; when none holds it, that is logged and nothing is routed blindly
- [ ] A test with two Drakes of one project, the task in the second, shows the second Drake handles it; red on the current code

## Out of scope

- The escalation routes themselves

## Human test plan

N/A — routing between in-memory Drakes, fully asserted by the test above.

## Implementation plan

_Populated by `/tasks plan TASK-169` — leave empty until then._
