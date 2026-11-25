---
id: TASK-216
parent: STORY-052
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-211]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Merging a feature updates the project changelog

## Context

FEATURE-078 D12. Merging a feature branch records its changes in the project changelog automatically.

Decisions: FEATURE-078 D12.

## Acceptance criteria

- [ ] Merging a feature adds its changes to the project changelog
- [ ] The changelog follows the standard format

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] Merge a feature on a throwaway project: the changelog gains its entry

## Implementation plan

_Populated by `/tasks plan TASK-216` — leave empty until then._
