---
id: TASK-131
parent: STORY-040
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-533 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use the Birko keyed broadcaster for run events; delete KoboldRunEventSource

## Context

`Services/KoboldRunEventSource.cs` (124) is the generic part; `RunRegistry` stays.

Framework side: Birko TASK-533 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Run events published through the framework broadcaster
- [ ] SSE / WebSocket run streams unchanged for clients (RunEventsSseTests green)
- [ ] `KoboldRunEventSource` deleted

## Out of scope

- The framework change itself (Birko TASK-533)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
