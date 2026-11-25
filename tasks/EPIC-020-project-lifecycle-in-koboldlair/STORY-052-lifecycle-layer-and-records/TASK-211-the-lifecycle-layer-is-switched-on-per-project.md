---
id: TASK-211
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
blocks: [TASK-212, TASK-213, TASK-214, TASK-215, TASK-216]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# The lifecycle layer is switched on per project

## Context

FEATURE-078 D5. A per-project switch: on by default for new projects, off for existing ones until adopted. The
question chain does not follow it (D27).

Decisions: FEATURE-078 D5.

## Acceptance criteria

- [ ] A project carries the switch; new projects start with it on, existing ones off
- [ ] The switch can be turned on from Dragon and the web UI
- [ ] Every lifecycle feature except the question chain checks it

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] A new project starts with the layer on; an existing one is off until switched on

## Implementation plan

_Populated by `/tasks plan TASK-211` — leave empty until then._
