---
id: TASK-201
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: [TASK-202, TASK-203, TASK-205, TASK-206, TASK-208, TASK-209]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: 73
github-issue: null
jira-key: null
---

# A question climbs the agent hierarchy one way, and its answer returns down the same path

## Context

The core of the question chain (FEATURE-078 D18, D24, D27). Today a Kobold's `ask_human` creates a pending decision for
the person directly (TASK-021); nothing in between can answer. This task adds the path itself: a question carries the
levels it has passed (worker → supervisor (Drake) → overseer (Wyvern) → Dragon → person), each level gets it once, it
only moves up, and the answer travels back down the same path to the asker. In this task no level answers yet: each
passes the question up, so behaviour for the person is unchanged; later tasks give each level its answering rule.
The chain works in every project, whatever the lifecycle switch says (D27).

Decisions: FEATURE-078 D18, D24, D27.

## Acceptance criteria

- [x] A question records each level it has passed; a level that already had it never gets it again, and it never moves down
- [x] With no level answering, a question reaches the person exactly as `ask_human`'s does today (card, Needs you, Dragon), and its answer reaches the asker
- [x] The answer returns along the recorded path, so every level that passed it sees the answer
- [x] The path survives a server restart (stored with the decision)
- [x] Works in every project; no per-project switch

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

N/A — the routing is asserted by tests; the person-facing behaviour is unchanged until the levels answer

## Implementation plan

Planned at pick (2026-11-25):

1. `QuestionLevel` (Worker, Supervisor, Overseer, Dragon, Person; stored numbers) and `QuestionChain` with
   `IQuestionLevel`: a question climbs from the level above its asker, the first level that answers stops it, a level
   with nothing registered or one that fails passes it on, and the person is the end; `DeliverAnswerAsync` takes the
   answer back down the levels below the answerer, nearest first.
2. `PendingDecision.Path`, stored as `PathJson` (the repository adds the column to an older table at startup).
3. `DrakeFactory.Chain`. The analyzer's questions (TASK-170) are the chain's first users: they climb from the Overseer;
   one answered on the way is stored answered with no card, so the analysis re-runs with it on the next cycle. A
   person's answer goes back down the stored path from `ResolvePendingDecisionAsync`.
4. Kobold's `ask_human` stays as it is; routing it is TASK-202.

## Notes

- **Red, after the fact** (code first again): with the routing in `DrakeFactory` replaced by a straight-to-person
  outcome and no delivery, the three chain tests in `WyvernQuestionsTests` failed; `QuestionChainTests` (10) are new
  with the type. Full .NET suite 673 passed; no client change.
- **Upgrade, seen live:** starting the throwaway server on the TASK-170 database logged "Added missing column
  pending_decisions.PathJson (TEXT)"; its two older decisions kept a null path and read as off the chain.
- No level is registered in the server yet, so the person sees no change; each level comes with its own task
  (TASK-203, 205, 206).
- A round answered entirely on the way re-runs the analysis on the next service cycle, not at once.
- `EventStoreMigrationTests.Two_writers_changing_one_specification_at_once_both_land_on_distinct_versions` failed once
  in one full run and passed alone and in the next full run: a concurrency flake, unrelated, not filed yet.
