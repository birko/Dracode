---
id: TASK-213
parent: STORY-052
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-211, TASK-212]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# A new project gets the full lifecycle layer; an imported one gets what it is missing

## Context

FEATURE-078 D4. A new project starts with readme, project guide, features, tasks and changelog; an imported
project gets only the parts it lacks.

Decisions: FEATURE-078 D4.

## Acceptance criteria

- [ ] A new switched-on project starts with the full layer
- [ ] Switching on an imported project adds only the parts it lacks and changes nothing it has

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] Switch on an imported project: its existing files are untouched and the missing ones appear

## Implementation plan

_Populated by `/tasks plan TASK-213` — leave empty until then._
