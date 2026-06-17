---
id: TASK-039
parent: STORY-015
feature: FEATURE-017
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-038]
blocks: [TASK-041]
pr: null
github-issue: null
jira-key: null
---

# /kobold ad-hoc mode

## Context

Ad-hoc mode runs a single Kobold against the caller's cwd: `{ mode: "adhoc", cwd, prompt, agentType? }`. Validates cwd, auto `git init` if not a repo, always creates a worktree under `<cwd>/.koboldlair/.worktrees/r-<runId>/`, spawns via `KoboldFactory`, streams via TASK-038. Off-registry run keyed by `runId`.

## Acceptance criteria

- [ ] cwd validated; if not a git repo, `git init` + initial commit of existing files (via `GitService`)
- [ ] Worktree created under `<cwd>/.koboldlair/.worktrees/r-<runId>/`
- [ ] Kobold spawned in the worktree via `KoboldFactory`; agent type from payload or auto-detected (decide per STORY-015 open question)
- [ ] Run tracked off-registry by `runId`; completion returns `{ status, worktree, runId }`
- [ ] Cleanup policy for stale ad-hoc worktrees after N days
- [ ] Tests: adhoc run against a temp dir produces a worktree + commit

## Out of scope

- Widening `PathHelper` for out-of-sandbox cwds (TASK-041)
- The `koboldlair merge` CLI verb (EPIC-013)

## Human test plan

- [ ] Point ad-hoc mode at a non-git temp folder with a file → confirm `.git/` + worktree created, Kobold edits land in the worktree, original cwd untouched until merge

## Implementation plan

_Populated by `/tasks plan TASK-039` — leave empty until then._
