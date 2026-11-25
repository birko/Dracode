---
id: TASK-212
parent: STORY-052
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-210, TASK-211]
blocks: [TASK-213]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# The plan is rendered in the standard lifecycle layout, read-only

## Context

FEATURE-078 D2, D3, D31. Each project's plan is written out as the standard layout — the project as an epic, each
feature as a story, each unit of work as a task with acceptance criteria, dependencies, priority and a human test plan.
The database stays the source of truth; the files are a one-way, read-only rendering. The current per-area task lists
are kept alongside until this ships.

Decisions: FEATURE-078 D2, D3, D31.

## Acceptance criteria

- [ ] A switched-on project's plan is rendered as epic, stories and tasks in the standard layout
- [ ] The rendering follows every change and is never read back
- [ ] The per-area task lists keep working beside it

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] Open a switched-on project's folder: its plan reads as epic, stories and tasks matching the Board

## Implementation plan

_Populated by `/tasks plan TASK-212` — leave empty until then._
