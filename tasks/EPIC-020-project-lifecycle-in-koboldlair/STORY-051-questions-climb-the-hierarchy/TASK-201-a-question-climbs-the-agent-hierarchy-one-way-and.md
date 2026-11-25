---
id: TASK-201
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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
pr: null
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

- [ ] A question records each level it has passed; a level that already had it never gets it again, and it never moves down
- [ ] With no level answering, a question reaches the person exactly as `ask_human`'s does today (card, Needs you, Dragon), and its answer reaches the asker
- [ ] The answer returns along the recorded path, so every level that passed it sees the answer
- [ ] The path survives a server restart (stored with the decision)
- [ ] Works in every project; no per-project switch

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

N/A — the routing is asserted by tests; the person-facing behaviour is unchanged until the levels answer

## Implementation plan

_Populated by `/tasks plan TASK-201` — leave empty until then._
