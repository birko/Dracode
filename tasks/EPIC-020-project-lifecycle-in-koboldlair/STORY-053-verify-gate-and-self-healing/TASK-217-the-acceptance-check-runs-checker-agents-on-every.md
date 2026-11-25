---
id: TASK-217
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: [TASK-218, TASK-219, TASK-220, TASK-221, TASK-222]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# The acceptance check runs checker agents on every task, cheapest first

## Context

FEATURE-078 D8 (changed), D10, D30. The acceptance check (TASK-025) already holds a finished task to its own
description before Done. It becomes the verify gate: purpose-built checker agents run on every task, cheapest first,
after its own checks. This task builds the runner and the checker contract; the checkers are their own tasks.

Decisions: FEATURE-078 D8, D10, D30.

## Acceptance criteria

- [ ] The acceptance check runs a list of checker agents after its own checks, cheapest first, on every task
- [ ] Each checker returns findings in one shape; none passes the task to Done while a finding is open
- [ ] With no checker registered, behaviour is today's

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

N/A — the runner is asserted by tests; each checker carries its own human test

## Implementation plan

_Populated by `/tasks plan TASK-217` — leave empty until then._
