---
id: TASK-041
parent: STORY-015
feature: FEATURE-017
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-039]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Widen PathHelper for ad-hoc cwds

> Unblocked 2026-06-23 — TASK-039 (its only `depends-on`) is code-complete (in review); ad-hoc mode now sets the Kobold's `WorkingDirectory` to the worktree, so this task hardens that scope in `PathHelper`.

## Context

Ad-hoc `/kobold` runs operate on arbitrary caller cwds, outside KoboldLair's normal per-project workspace sandbox. `PathHelper` validates all file ops against workspace + allowed paths; it must be widened (or explicitly scoped) for ad-hoc runs **without** weakening sandboxing for the project pipeline.

## Acceptance criteria

- [ ] Ad-hoc runs may read/write within their `cwd` worktree; project-pipeline sandboxing is unchanged
- [ ] The widened path scope is bounded to the ad-hoc run's worktree, not "any path"
- [ ] Attempts to escape the ad-hoc worktree (e.g. `../../`) are still rejected
- [ ] Tests: project-mode path validation unchanged; ad-hoc allows its worktree, rejects traversal

## Out of scope

- General reform of the sandbox model
- The ad-hoc run flow itself (TASK-039)

## Human test plan

- [ ] Run ad-hoc against a folder, confirm the Kobold can write inside its worktree but a crafted traversal path is rejected

## Implementation plan

_Populated by `/tasks plan TASK-041` — leave empty until then._
