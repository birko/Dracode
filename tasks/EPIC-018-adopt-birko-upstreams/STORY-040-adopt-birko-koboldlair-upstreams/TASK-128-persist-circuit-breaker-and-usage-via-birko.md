---
id: TASK-128
parent: STORY-040
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-530 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Persist circuit-breaker state and usage through Birko.AI.Resilience stores

## Context

`Program.cs` builds `new ProviderCircuitBreaker(logger: logger)` with no store and logs "in-memory state persistence" — breaker state is lost on restart. `SqlUsageRepository.cs` (171) + `UsageRecordEntity.cs` (66) aggregate in memory. EPIC-016's table claims circuit-breaker state is in the DB; it is not.

Framework side: Birko TASK-530 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Breaker uses the framework store; state survives a restart
- [ ] Usage via the framework store; `SqlUsageRepository` and both entities deleted
- [ ] EPIC-016's table corrected

## Out of scope

- The framework change itself (Birko TASK-530)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
