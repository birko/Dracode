---
id: TASK-202
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

# A Kobold's ask_human goes through the chain

## Context

FEATURE-078 D23. `ask_human` (TASK-021) parks the task and stores a decision for the person. With the chain
(TASK-201), the question starts at the worker's level and climbs; the task waits, parked as today, while it climbs.

Decisions: FEATURE-078 D23.

## Acceptance criteria

- [ ] `ask_human` puts its question on the chain instead of straight to the person
- [ ] The task stays parked (AwaitingHumanDecision) while the question climbs, and resumes with the answer as today (TASK-022)
- [ ] A question answered by an agent on the way resumes the task the same way as a person's answer

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] On a throwaway server, a Kobold's question reaches the person through the chain, and the answer resumes the task

## Implementation plan

_Populated by `/tasks plan TASK-202` — leave empty until then._
