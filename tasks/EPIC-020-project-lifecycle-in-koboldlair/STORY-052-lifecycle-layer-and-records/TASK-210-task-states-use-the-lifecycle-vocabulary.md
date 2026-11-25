---
id: TASK-210
parent: STORY-052
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: [TASK-212]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Task states use the lifecycle vocabulary

## Context

FEATURE-078 D1. Task states become to do · in progress · verify · done · cancelled, with "blocked" as a flag on a
task rather than a state. Task states are stored as numbers, so new members are appended, never renumbered.

Decisions: FEATURE-078 D1.

## Acceptance criteria

- [ ] Tasks can be in verify and cancelled; blocked is a flag a task carries in any state
- [ ] Stored task states keep their numbers; existing data reads the same
- [ ] The web UI, Dragon and the task files show the new vocabulary

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] The Board shows a task in verify and a blocked task

## Implementation plan

_Populated by `/tasks plan TASK-210` — leave empty until then._
