---
id: TASK-129
parent: STORY-040
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-531 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use classified LLM failures; retire ErrorClassifier's substring matching

## Context

`Services/ErrorClassifier.cs` (168) decides retry vs fail from error text ("500", "timeout", "quota exceeded" all misfire). Used by `UpdatePlanStepTool`, `TaskTracker`, `FailureRecoveryJob`.

Framework side: Birko TASK-531 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Retry decisions read the response's classification
- [ ] `ErrorClassifier` deleted, or kept only for tool-output text
- [ ] Retry-after honoured by the recovery job

## Out of scope

- The framework change itself (Birko TASK-531)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
