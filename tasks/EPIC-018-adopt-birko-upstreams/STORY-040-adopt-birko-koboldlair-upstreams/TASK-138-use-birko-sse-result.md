---
id: TASK-138
parent: STORY-040
feature: null
status: todo
priority: P3
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-540 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Stream run events with the Birko SSE result

## Context

`Api/RunsEndpoints.cs` L134–194 (`StreamRunEventsAsync`, `WriteFrameAsync`).

Framework side: Birko TASK-540 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Endpoint returns the framework result
- [ ] Local frame writer deleted
- [ ] `RunEventsSseTests` green; TASK-107 (nginx check) still applies

## Out of scope

- The framework change itself (Birko TASK-540)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
