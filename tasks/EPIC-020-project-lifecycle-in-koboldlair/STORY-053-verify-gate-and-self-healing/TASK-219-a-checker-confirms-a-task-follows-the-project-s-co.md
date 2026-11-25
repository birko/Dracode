---
id: TASK-219
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-217, TASK-215]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# A checker confirms a task follows the project's conventions

## Context

FEATURE-078 D8. Checks a task's changes against the conventions Wyrm recorded (TASK-215).

Decisions: FEATURE-078 D8.

## Acceptance criteria

- [ ] The checker reports each convention a change breaks, naming the convention

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] A change against a recorded convention is held with a finding naming it

## Implementation plan

_Populated by `/tasks plan TASK-219` — leave empty until then._
