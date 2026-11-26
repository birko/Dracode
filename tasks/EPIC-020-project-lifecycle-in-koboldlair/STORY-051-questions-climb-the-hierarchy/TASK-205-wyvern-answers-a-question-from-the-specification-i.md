---
id: TASK-205
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-201, TASK-204]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: 80
github-issue: null
jira-key: null
---

# Wyvern answers a question from the specification, its analysis and the decisions, or passes it up

## Context

FEATURE-078 D18, the overseer's level. Wyvern answers only from the specification, its analysis and the decision
ledger; anything else goes up to Dragon's level.

Decisions: FEATURE-078 D18.

## Acceptance criteria

- [x] Wyvern answers a question those sources settle, naming which settled it
- [x] A question they do not settle goes up the chain
- [x] An answer it gives is recorded (TASK-208)

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] A question the specification answers is answered by Wyvern without reaching the person

## Implementation plan

Planned at pick (2026-11-26):

1. `OverseerLevel`, a `QuotedAnswerLevel` like Drake's and Dragon's: its sources are the project's `specification.md`,
   Wyvern's `analysis.md` and the decisions answered in the project; a question already decided there needs no model;
   otherwise a quoted answer, kept only when the quote is in the sources ("from the specification, the analysis or a
   decision: …").
2. The server registers it with Wyvern's model (`wyvern-answer`).
3. A quoted answer over fixed sources, not TASK-204's multi-turn `ConsultAsync`: a quote can only be checked against
   sources the level hands the model, and a free look-up could quote any file.

## Notes

- **An answer it gives is recorded:** like every level's, as a decision stored answered with its path (TASK-201/202);
  showing it to the person and overturning it is TASK-208.
- 4 `OverseerLevelTests` (written with the class; red only as a compile failure); full .NET suite 702 passed; no
  client change.
- **Human test** (throwaway server, vLLM `lan2`), project `banner205`: the spec says "The banner colour is teal." and
  that the worker looks the colour up with `ask_human`.
  - Wyvern asked it first, during its analysis (a project-level question, path Dragon): **Dragon** answered "teal",
    quoting "The banner colour is teal." — the live Dragon answer TASK-206's human test had missed.
  - The Kobold then asked the same question (path Drake → Wyvern): Drake declined, and **Wyvern** answered "teal"
    from the project's decision record; it never reached Dragon or the person. The task finished and `banner.py`
    prints "KOBOLD [teal]".
  - Wyvern's quoted answer (not from a record) was not seen live; the tests cover it.
