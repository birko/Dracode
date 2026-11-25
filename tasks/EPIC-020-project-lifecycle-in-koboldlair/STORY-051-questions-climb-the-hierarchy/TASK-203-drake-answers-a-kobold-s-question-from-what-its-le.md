---
id: TASK-203
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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
pr: null
github-issue: null
jira-key: null
---

# Drake answers a Kobold's question from what its level owns, or passes it up

## Context

FEATURE-078 D18, the supervisor's level. Drake answers only from the task's description, its plan, the project's
constraints and earlier answers; anything else goes up the chain (TASK-201). An answer it gives is recorded (TASK-208).

Decisions: FEATURE-078 D18.

## Acceptance criteria

- [ ] Drake answers a question its task, plan, constraints or earlier answers settle, naming which of them settled it
- [ ] A question they do not settle goes up the chain unchanged
- [ ] Drake never answers from its own judgement beyond those sources

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] A question the plan already answers is answered by Drake without reaching the person; one it does not is passed up

## Implementation plan

_Populated by `/tasks plan TASK-203` — leave empty until then._
