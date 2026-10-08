---
id: TASK-136
parent: STORY-040
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-538 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use Birko.BackgroundJobs hosting and registration; delete the two hosted-service bridges

## Context

`Jobs/BackgroundJobProcessorHostedService.cs` (29), `Jobs/RecurringJobSchedulerHostedService.cs` (23) and the wiring in `Program.cs` L683–708.

Framework side: Birko TASK-538 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Registered via `AddBackgroundJobs` / `AddRecurringJob<T>`
- [ ] Both bridges deleted
- [ ] `FailureRecoveryJob` still runs on schedule

## Out of scope

- The framework change itself (Birko TASK-538)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
