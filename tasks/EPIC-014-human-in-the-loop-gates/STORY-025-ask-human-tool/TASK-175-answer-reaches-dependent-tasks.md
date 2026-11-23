---
id: TASK-175
parent: STORY-025
feature: FEATURE-028
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
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
pr: 47
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

- [x] FEATURE-028 has a decision on how a person's answer reaches other tasks (the `proposed` row filed with this task),
      settled through `/feature decide` before any code changes
- [x] Once decided: a test shows a Kobold summoned for a task whose dependency has an answered decision hears that
      answer, attributed to the person and to the decision, and fails without the change
- [x] The full suite passes

## Out of scope

- Wyvern asking questions itself (FEATURE-028 D7, TASK-170).
- How Wyvern splits a question into its own task.

## Human test plan

- [x] Re-run the greeter scenario (a spec with one owner question, split into a question task and a dependent task);
      expected: the person gets one card, and the dependent task uses the answer without asking again.

## Implementation plan

Decided first: FEATURE-028 D8, `changed` by the owner 2026-11-23 — every task that depends on the asking task, directly
or through others; whatever a person wrote is passed on; the dependent hears it named as the other task's.

1. `HumanAnswer` gains `FromTask` (the asking task's first line) and speaks as another task's decision when set;
   `HumanAnswer.CarriesPersonsWords` says which resolved decisions are passed on: answers, typed escalation
   instructions, approval remarks and rejection reasons — not timeouts, "handle it automatically", a bare resume or a
   bare approval.
2. `Drake.LoadHumanAnswersAsync` walks the task's dependencies (bracketed ids, as the readiness check reads them)
   through chains and across the project's area task files, and puts what those tasks' decisions pass on ahead of the
   task's own answers, oldest first, under each decision's own tag; it logs when it passes any on.
3. `Kobold`: the fresh prompt's header names the tasks it depends on when it holds their answers, and an inherited answer
   alone no longer makes a first-step run resume a checkpoint (the task never asked).
4. Tests (`DependentTaskAnswersTests`): same-area direct dependent; a task two steps away in another area; only what a
   person wrote is passed on.

## Notes

- The tests were red before the change (each dependent heard nothing); 582/582 .NET and 154/154 e2e green after.
- **Human test, 2026-11-23**, on a throwaway server (scratch database, LAN `finstat` model). Wyvern split the greeter
  spec as in the original run, over three areas: `greeter-1` asks, `greeter-2` depends on it, `docs-1` (another area)
  on both, `qa-1` (a third area) on `greeter-2` and `docs-1` only. `greeter-1` called `ask_human` once; its card was
  answered in the web dock (one card, two options, "Slovak"); all four tasks finished Done with **no second
  `ask_human` call and no second card**, and `greeter.js` returns "Ahoj, <name>!". Prompts are not stored, so the
  delivery was confirmed by replaying the run's own task files and decision store through `LoadHumanAnswersAsync`:
  `greeter-2`, `docs-1` and `qa-1` each hear the Slovak answer once, named as `greeter-1`'s question; `greeter-1` hears
  it as its own. (In this run `greeter-1` also wrote the answer into a config file, which the model may have read too —
  hence the replay, and the log line added for future runs.)
- Seen on the way, already filed: a run that stops itself after `ask_human` logs "reached iteration limit" (TASK-191).
- FEATURE-028's `status.md` is generated and was already stale; re-run `/feature status FEATURE-028` to refresh it.
