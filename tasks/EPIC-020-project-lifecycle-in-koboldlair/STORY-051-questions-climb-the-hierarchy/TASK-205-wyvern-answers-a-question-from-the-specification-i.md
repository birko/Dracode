---
id: TASK-205
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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
pr: null
github-issue: null
jira-key: null
---

# Wyvern answers a question from the specification, its analysis and the decisions, or passes it up

## Context

FEATURE-078 D18, the overseer's level. Wyvern answers only from the specification, its analysis and the decision
ledger; anything else goes up to Dragon's level.

Decisions: FEATURE-078 D18.

## Acceptance criteria

- [ ] Wyvern answers a question those sources settle, naming which settled it
- [ ] A question they do not settle goes up the chain
- [ ] An answer it gives is recorded (TASK-208)

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] A question the specification answers is answered by Wyvern without reaching the person

## Implementation plan

_Populated by `/tasks plan TASK-205` — leave empty until then._
