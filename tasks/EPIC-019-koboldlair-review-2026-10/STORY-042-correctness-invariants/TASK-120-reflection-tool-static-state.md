---
id: TASK-120
parent: STORY-042
feature: null
status: done
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

- [x] All per-run state is instance state, supplied when the Kobold's tool set is built
- [x] No static mutable fields remain in the tool
- [x] Test: two Kobolds reflecting concurrently each write to their own plan and raise their own escalation

## Human test plan

N/A — covered by the concurrency test.

## Implementation plan

Done 2026-10-19.

Widened to all three plan tools: `UpdatePlanStepTool` and `ModifyPlanTool` had the same static `RegisterContext` /
`ClearContext` shape and are wired at the same two `Kobold.cs` call sites, so fixing only `reflect` would have left
`update_plan_step` recording steps on the wrong plan. Worse than the review noted: a Kobold finishing called
`ClearContext()`, which wiped the context of every other Kobold still running.

1. `ReflectionTool`, `UpdatePlanStepTool`, `ModifyPlanTool`: per-run state is `readonly` instance fields set by a
   constructor that takes what `RegisterContext` took; the lock is per instance. `RegisterContext`, `ClearContext` and
   the unused `UpdatePlanStepTool.GetCurrentPlan` deleted, with the now-unreachable "no plan is active" branches.
2. `Kobold.cs` (planned and planless-with-plan paths): `Agent.AddTool(new …Tool(context))`; cleanup only removes the tools.
3. Test: `PlanToolIsolationTests` — two runs reflecting 25 times each at once keep their own reflections, plan
   escalations and callbacks; two runs updating steps at once each change only their own plan.

## Close notes

- Closed 2026-10-19. Suite 267/267.
- Shown red on the old code with the same scenario against the static API: Kobold A registers, Kobold B registers,
  A reflects — A's plan got 0 reflections (they went to B's).
