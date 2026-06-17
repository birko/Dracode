---
id: TASK-040
parent: STORY-015
feature: FEATURE-017
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-038]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# /kobold project mode

## Context

Project mode runs a Kobold against an already-analyzed project task: `{ mode: "project", projectId, taskId }`. Loads the existing plan + worktree from project state (reuses Drake's worktree machinery), streams identically to ad-hoc, and on completion commits to the project's feature branch (existing Drake behaviour).

## Acceptance criteria

- [ ] Loads the task's existing plan + feature-branch worktree (`Drake.SetupFeatureBranchWorktreeAsync` reuse)
- [ ] Spawns Kobold against that plan; streams via TASK-038
- [ ] On completion, commits to the feature branch (existing `Drake.CommitTaskCompletionAsync` path) and cleans up the worktree
- [ ] Respects per-project parallel Kobold limits and execution state (Paused/Suspended)
- [ ] Tests: project-mode run on a seeded analyzed project commits to the right branch

## Out of scope

- Ad-hoc mechanics (TASK-039)
- Changing Drake's background execution loop

## Human test plan

- [ ] Run project mode against a seeded analyzed project/task → confirm the commit lands on the task's feature branch and the worktree is cleaned up

## Implementation plan

_Populated by `/tasks plan TASK-040` — leave empty until then._
