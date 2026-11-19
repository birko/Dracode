---
id: TASK-175
parent: STORY-025
feature: FEATURE-028
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-17
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-022]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-025]
pr: null
github-issue: null
jira-key: null
---

# A task that depends on an answered question asks the person the same question again

## Context

Found 2026-11-17 in an end-to-end run against a real, throwaway server (built from `task/TASK-025`, `c3d2c8f`, LAN
`finstat` model, acceptance check on). The spec told the implementer to ask the owner, with `ask_human`, whether
greetings are English or Slovak. Wyvern split it into `decision-1` ("Ask owner to choose greeting language") and
`backend-1` ("Implement greeter.js using owner-selected language", depends on `decision-1`).

- `decision-1`'s Kobold asked, the person answered "Slovak (Ahoj, <name>!)", the task resumed with the answer and
  finished Done.
- `backend-1`'s Kobold then found no record of the decision ("No separate decision-1 record file exists") and called
  `ask_human` with the same question. A second card reached the person, who had to answer it again.

**Mechanism.** TASK-022 delivers answers per task: `Drake.SummonKoboldAsync` hands a Kobold its own task's answered
and timed-out decisions (`Kobold.HumanAnswers`, from `GetResolvedForTaskAsync(taskId)`). A task's dependencies' answers
are never handed on, so a person's decision binds only the task that asked it. FEATURE-028 D6 decided delivery to the
asking agent; nothing decided what other tasks in the project see.

## Acceptance criteria

- [ ] FEATURE-028 has a decision on how a person's answer reaches other tasks (the `proposed` row filed with this task),
      settled through `/feature decide` before any code changes
- [ ] Once decided: a test shows a Kobold summoned for a task whose dependency has an answered decision hears that
      answer, attributed to the person and to the decision, and fails without the change
- [ ] The full suite passes

## Out of scope

- Wyvern asking questions itself (FEATURE-028 D7, TASK-170).
- How Wyvern splits a question into its own task.

## Human test plan

- [ ] Re-run the greeter scenario (a spec with one owner question, split into a question task and a dependent task);
      expected: the person gets one card, and the dependent task uses the answer without asking again.

## Implementation plan

_Populated by `/tasks plan TASK-175` — leave empty until then._
