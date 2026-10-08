---
id: TASK-132
parent: STORY-040
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-534 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use the Birko git service and tools; delete GitService and the four git tools

## Context

`Services/GitService.cs` (765), `Models/Git/GitBranch.cs` (141), `GitStatusTool`, `GitDiffTool`, `GitCommitTool`, `GitMergeTool`. ⚠ `RunGitCommandAsync` can deadlock on a large stderr and has no timeout — live until this lands.

Framework side: Birko TASK-534 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Drake commit / merge / worktree flow on the framework service
- [ ] Sentinel's git tools are the framework tools, wired with DraCode's project lookup
- [ ] `CreateFeatureBranchName` stays in DraCode
- [ ] Local files deleted

## Out of scope

- The framework change itself (Birko TASK-534)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
