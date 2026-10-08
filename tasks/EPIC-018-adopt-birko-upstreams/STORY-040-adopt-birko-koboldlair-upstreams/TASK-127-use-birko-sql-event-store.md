---
id: TASK-127
parent: STORY-040
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-529 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use the framework SQL event store; delete SqlEventStoreRepository and DomainEventEntity

## Context

`Data/Repositories/Sql/SqlEventStoreRepository.cs` (141) + `Data/Entities/DomainEventEntity.cs` (72): not atomic, no version uniqueness, version read in memory. `_loose/TASK-080` (rename `AggregateId`) is superseded if the framework owns the entity.

Framework side: Birko TASK-529 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Specification event sourcing writes through the Birko store
- [ ] Existing events in `koboldlair.db` are readable (same layout, or a migration)
- [ ] `_loose/TASK-080` closed as superseded or done here

## Out of scope

- The framework change itself (Birko TASK-529)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
