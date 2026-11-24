---
id: TASK-182
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-21
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-032]
pr: 52
github-issue: null
jira-key: null
---

# A finished task is escalated as "progress stalled" and its plan revised after it is Done

## Context

Found 2026-11-21 in TASK-153's human test (throwaway server, scratch `projects153`, LAN `finstat` model). `waver2`'s
`impl-1` was retried from the Board; its Kobold resumed the saved plan, finished the last step and completed in 6.6 s
(`[Kobold] COMPLETE f434d5e1`). After that the log shows `Escalation 6355eebd resolved: Plan revised with 4 new/revised
steps` and a project notification `[WrongApproach] Task f434d5e1: Progress stalled: last 3 reflections show no
advancement (100→100→100%)`. The task stayed Done.

Two things look wrong:

- **100% counts as stalled.** `ReflectionTool`'s stall check (`Progress stalled: last {n} reflections show no
  advancement`) fires on flat progress even when it is flat at 100%, which means done, not stuck.
- **An escalation reaches Drake after the task is Done.** `HandleEscalationAsync` routed it through WrongApproach and
  revised the plan of a finished task: wasted LLM work, a misleading notification to the person, and a plan that no
  longer matches what was built.

## Acceptance criteria

- [x] Reflections flat at 100% do not raise a stall escalation (a test that fails today)
- [x] An escalation for a task that is Done when Drake handles it is dropped (logged), with no plan revision and no
      notification (a test that fails today)

## Human test plan

- [x] Retry a failed task whose plan is nearly finished on a throwaway server: it completes with no escalation in the log
      and no escalation notification

## Implementation plan

1. Red first:
   - `ReflectionStallTests`: flat at 100% raises no stall; flat at 60% still does.
   - `TaskAcceptanceGateTests`: an escalation for a Done task revises nothing and notifies nobody.
   - `TaskAcceptanceGateTests`: a task that becomes Done while its plan is being revised (the scripted planner marks
     it Done on its first call) keeps its finished plan, and nobody is notified. This case came from the human test.
2. `ReflectionTool`: a stall needs the last progress below 100.
3. `Drake.HandleEscalationAsync`:
   - An escalation for a Done task is dropped (`DropEscalation`: resolved, logged, no notification).
   - The same check runs again when the planner returns from a WrongApproach revision.
   - The planner now revises a copy (`CopyOfPlan`). A kept revision is applied to the live plan in place
     (`ApplyRevision`), because a running Kobold's plan tools hold that object. `RevisePlanAsync` rewrites the plan it
     is given, so before this a dropped revision would still have changed the live plan.

## Notes

- Human test on a throwaway server (copy of the TASK-175 `greeter175` project, LAN `finstat` model). `qa-1` was set
  Failed with its last plan step reopened, then retried through `POST /api/v1/tasks/{id}/retry`:
  - Before the fix it reproduced the incident's other half. The reflections went 97→80→85, a real stall below 100.
    The revision returned after the task was Done, replaced the finished plan ("Plan revised with 8 new/revised
    steps") and notified the person. That run led to the second check and to revising a copy.
  - After the fix, on a fresh copy, the reflections went 97→75→90. The revision returned while the task still ran, so
    it was applied correctly and its notification is legitimate. The Kobold then worked the revised 14-step plan to
    Done. This confirms a kept revision still reaches the running Kobold. The race did not recur; the new test pins it.
  - The plan's "no escalation in the log" depends on the model, which stalled below 100% both times. Flat at 100% was
    not reproduced live; `ReflectionStallTests` pins it.
- Found while setting up the test: retrying a task of a project marked Completed resets the task, but no Drake
  runs it, because `DrakeExecutionService` only takes Analyzed and InProgress projects. The project had to be set
  back to InProgress by hand. Filed as TASK-196.
- Full .NET suite: 611 passed. The e2e suite was not run: nothing the web client reaches changed.
