---
id: TASK-195
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-23
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-041]
pr: 50
github-issue: null
jira-key: null
---

# Two planners running at once can take each other's plans

## Context

Found 2026-11-23 while writing TASK-173's tests. `CreateImplementationPlanTool` keeps the plan the model submitted in a
process-wide static (`_lastPlan`, behind a lock). `KoboldPlannerAgent` clears it before a run (`ClearLastPlan`) and
reads it back after (`GetLastPlan`), both for a new plan and for `RevisePlanAsync`.

The server runs planners concurrently: Drakes of different projects, and different areas of one project, plan in
parallel, and a WrongApproach revision can run beside a new plan. So one planner's clear can erase another's result
(that planner then falls back to "no plan" or, for a revision, keeps the old plan), and one planner can read back
another's plan — a task then runs steps written for a different task. Nothing logs it; the wrong plan looks valid.

## Acceptance criteria

- [x] A plan the model submits reaches only the planner run that asked for it: the result is held per run (e.g. on the
      tool instance the planner registers, or returned from its run), never in process-wide state
- [x] A test runs two planners at once with different scripted plans and shows each gets its own, red today
- [x] The full suite passes

## Out of scope

- How the planner prompts or validates a plan.

## Human test plan

N/A — the isolation is shown by the concurrent test.

## Implementation plan

1. Red first: `PlannerRunIsolationTests` runs one shared `KoboldPlannerAgent` twice at once with a scripted provider
   that holds run A's closing turn until run B has submitted its plan. Two cases: two new plans, and a WrongApproach
   revision beside a new plan. Both failed before the fix, with run A reading B's plan ("Write B.cs").
2. `CreateImplementationPlanTool`: dropped the static `_lastPlan`, its lock, and `GetLastPlan` / `ClearLastPlan`.
   `Capture()` opens a `PlanCapture` held in an `AsyncLocal`, which flows with the run's awaits into the tool call.
   The tool writes the plan to the current capture.
3. `KoboldPlannerAgent.CreatePlanAsync` / `RevisePlanAsync` each open their own capture (`using var capture`) and read
   `capture.Plan` after the run.
4. `docs/Kobold-Planner-Agent.md` describes the per-run result.

## Notes

- Holding the plan on the tool instance would not have been enough. `DrakeFactory` creates one planner per Drake, and
  that Drake's Kobolds (up to `maxParallel`) call it at the same time, so the runs share the agent and its tools.
  The agent's run loop itself keeps its conversation local, so the plan was the only shared state.
- A tool call outside any capture stores nothing. Only the planner opens one.
- Full .NET suite: 601 passed. The e2e suite was not run: nothing the web client reaches changed.
