---
id: TASK-182
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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
pr: null
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

- [ ] Reflections flat at 100% do not raise a stall escalation (a test that fails today)
- [ ] An escalation for a task that is Done when Drake handles it is dropped (logged), with no plan revision and no
      notification (a test that fails today)

## Human test plan

- [ ] Retry a failed task whose plan is nearly finished on a throwaway server: it completes with no escalation in the log
      and no escalation notification

## Implementation plan

_Populated by `/tasks plan TASK-182` — leave empty until then._
