---
id: TASK-120
parent: STORY-042
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
findings: [CR-1]
---

# ReflectionTool keeps the current plan, task and Kobold in static fields, so parallel Kobolds overwrite each other

## Context

`DraCode.KoboldLair/Agents/Tools/ReflectionTool.cs` (365) holds `_currentPlan`, `_planService`, `_currentProjectId`,
`_currentTaskId`, `_koboldId`, `_agentType`, `_onEscalation`, `_config` and a semaphore as `private static` (L17–26).
With two Kobolds running, the second one's context replaces the first's: a reflection from Kobold A is recorded
against Kobold B's plan and escalates on B's callback. Per-run state on a process-wide object — the shape of Birko's
rule 36.

Found in the 2026-10-08 KoboldLair review.

## Acceptance criteria

- [ ] All per-run state is instance state, supplied when the Kobold's tool set is built
- [ ] No static mutable fields remain in the tool
- [ ] Test: two Kobolds reflecting concurrently each write to their own plan and raise their own escalation

## Human test plan

N/A — covered by the concurrency test.

## Implementation plan
