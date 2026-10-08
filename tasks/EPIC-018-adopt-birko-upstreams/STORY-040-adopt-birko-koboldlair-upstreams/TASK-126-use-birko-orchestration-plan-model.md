---
id: TASK-126
parent: STORY-040
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-528 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use Birko.AI.Orchestration's plan model, analyzer and dispatcher; migrate stored plan statuses

## Context

DraCode forks `ITaskDispatcher` / `DirectTaskDispatcher`, `StepDependencyAnalyzer`, `EscalationAlert`, `ReflectionEntry`, `TaskAssignment`, and keeps `KoboldImplementationPlan.cs` (735) plus the step validators. ⚠ Plan and step status are stored as integers and the enum orders differ (`StepStatus` …Skipped, Failed vs Birko …Failed, Skipped; `PlanStatus` has `Planning, Ready` that Birko lacks) — a type swap without a mapping turns every Failed step into Skipped.

Framework side: Birko TASK-528 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Local forks deleted; KoboldLair uses the Birko types
- [ ] Stored plans migrated with an explicit value mapping (DB rows and JSON plan files), tested on a copy of real data
- [ ] `QueueTaskDispatcher` moved or deleted per the framework task
- [ ] Plan / step tests green

## Out of scope

- The framework change itself (Birko TASK-528)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
