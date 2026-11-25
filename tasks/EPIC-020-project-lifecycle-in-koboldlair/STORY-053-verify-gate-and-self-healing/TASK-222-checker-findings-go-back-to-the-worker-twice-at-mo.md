---
id: TASK-222
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-217]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Checker findings go back to the worker, twice at most, then escalate

## Context

FEATURE-078 D9, D29. A task whose checks fail goes back to its worker with the findings; after two such reopens
the third failure escalates, the same limit the acceptance check uses today.

Decisions: FEATURE-078 D9, D29.

## Acceptance criteria

- [ ] Findings reopen the task with the findings in the worker's context
- [ ] The third failure escalates instead of reopening
- [ ] One limit covers the acceptance check and the checkers

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] A task failing a check three times escalates with its findings

## Implementation plan

_Populated by `/tasks plan TASK-222` — leave empty until then._
