---
id: TASK-207
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-206]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# What the person defines is written into the specification as a new version

## Context

FEATURE-078 D20. When the person defines something the specification lacked, Dragon writes it into the
specification as a new version (specifications already keep versions), so the next question about it is answered from
the specification and Kobolds see the change.

Decisions: FEATURE-078 D20.

## Acceptance criteria

- [ ] The person's definition is added to the specification as a new version, with its history
- [ ] The same question asked again is answered from the specification without reaching the person
- [ ] Running Kobolds see the specification change as they do today (version check)

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] Define something through a question on a throwaway server; the specification's history shows the new version

## Implementation plan

_Populated by `/tasks plan TASK-207` — leave empty until then._
