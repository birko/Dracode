---
id: TASK-139
parent: STORY-040
feature: null
status: todo
priority: P3
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-541 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use the Birko owner-or-admin check; delete ApiOwnership

## Context

`Api/ApiOwnership.cs` (30), used ~15 times in `ResourceEndpoints.cs` and in `RunsEndpoints`.

Framework side: Birko TASK-541 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] All call sites use the framework predicate
- [ ] `ApiOwnership` deleted
- [ ] Ownership tests green (404 for non-owners, admin sees all)

## Out of scope

- The framework change itself (Birko TASK-541)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
