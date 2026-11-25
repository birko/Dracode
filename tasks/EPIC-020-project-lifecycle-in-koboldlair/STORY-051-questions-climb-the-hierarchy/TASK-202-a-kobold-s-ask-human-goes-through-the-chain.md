---
id: TASK-202
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-201]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: 74
github-issue: null
jira-key: null
---

# A Kobold's ask_human goes through the chain

## Context

FEATURE-078 D23. `ask_human` (TASK-021) parks the task and stores a decision for the person. With the chain
(TASK-201), the question starts at the worker's level and climbs; the task waits, parked as today, while it climbs.

Decisions: FEATURE-078 D23.

## Acceptance criteria

- [x] `ask_human` puts its question on the chain instead of straight to the person
- [x] The task stays parked (AwaitingHumanDecision) while the question climbs, and resumes with the answer as today (TASK-022)
- [x] A question answered by an agent on the way resumes the task the same way as a person's answer

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] On a throwaway server, a Kobold's question reaches the person through the chain, and the answer resumes the task

## Implementation plan

Planned at pick (2026-11-25):

1. `Drake.AskHumanAsync` asks `QuestionChain` from the Worker level after the policy checks. A question no level
   answers parks the task exactly as before, its decision carrying the path. One a level answers is stored answered
   (task id, path), the levels below the answerer hear it, and `AskHumanOutcome` returns it with the answering level.
2. `AskHumanTool` returns such an answer to the Kobold at once ("Answered … by the supervisor …: X. Continue with this
   answer."), logs it in the plan, and does not stop the run. Its description says the question goes up the hierarchy
   first.
3. `DrakeFactory` hands each Drake its chain; a person's answer already goes back down the path (TASK-201).

## Notes

- **Criterion 3, read as built:** an answer given on the way never parks the task, so there is nothing to resume; the
  Kobold gets it as the tool's result in the same turn and continues. It is recorded as a decision like a person's.
- **Red, after the fact:** with the chain call replaced by a straight-to-person outcome, all three `AskHumanChainTests`
  failed. Full .NET suite 677 passed; no client change.
- The per-task question cap (`MaxQuestionsPerTask`) counts questions answered on the way too.
- **Human test** (throwaway server, vLLM `lan2`): a project `greet202` whose spec tells the worker to ask English or
  Slovak with `ask_human`, approved in Dragon. The Kobold asked "Which language should the greeting in greet.py use?";
  its decision carried the task id and the path Drake → Wyvern → Dragon → person, and the task parked with no timeout.
  Answered "Slovak" in Dragon: the task resumed and finished Done, and `greet.py` prints "Ahoj".
