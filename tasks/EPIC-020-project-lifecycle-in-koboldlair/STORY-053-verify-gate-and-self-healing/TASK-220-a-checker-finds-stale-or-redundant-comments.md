---
id: TASK-220
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
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

# A checker finds stale or redundant comments

## Context

FEATURE-078 D8. Finds comments whose content lives elsewhere or no longer matches the code (the lifecycle's
review-comments).

Decisions: FEATURE-078 D8.

## Acceptance criteria

- [ ] The checker reports each stale or redundant comment with where its content already lives

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] A comment that contradicts the code is held with a finding

## Implementation plan

_Populated by `/tasks plan TASK-220` — leave empty until then._
