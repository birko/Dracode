---
id: TASK-168
parent: STORY-042
feature: null
status: todo
priority: P1
assignee: agent
created: 2026-11-16
depends-on: []
blocks: []
findings: [FIELD-022]
pr: null
github-issue: null
jira-key: null
---

# A task's worktree is force-removed after its run even when the work was never committed

## Context

Found while planning TASK-020 (2026-11-16). After a Kobold run ends, `Drake.ExecuteTaskAsync`
(`DraCode.KoboldLair/Orchestrators/Drake.cs` ~2064–2081) syncs the task, which commits only when the task completed,
and then calls `CleanupWorktreeAsync`, which runs `git worktree remove <path> --force`
(`DraCode.KoboldLair/Services/GitService.cs` ~641). The worktree is kept only when the commit itself failed
(`task.CommitFailed`).

So every run that ends without a commit loses its worktree and the uncommitted files in it:

- **Server shutdown:** the run is cancelled; the plan paths save the plan and leave the Kobold `Working`; nothing is
  committed; the worktree, with the work done so far, is force-removed. On restart the task resumes from its plan
  step, but the files those steps wrote are gone.
- **A failed run:** the same, for whatever the Kobold wrote before failing (a retry starts from scratch).

TASK-020 keeps the worktree for a parked task (`AwaitingHumanDecision`) — that case is covered there. This task is
the rest.

## Acceptance criteria

- [ ] A run that ends without committing (cancelled, failed, or not Done for any reason) keeps its worktree, so a resumed run continues on its files
- [ ] Worktrees are still removed after a successful commit, and an abandoned one is still cleaned up by some path (say which — e.g. startup prune, task cancellation)
- [ ] A test pins the shutdown case: cancelled run → worktree still there; red on the current code

## Out of scope

- The parked-task case (TASK-020)

## Human test plan

- [ ] Start a project task, stop the server mid-run, restart it: the task resumes and the files written before the stop are still in its worktree

## Implementation plan

_Populated by `/tasks plan TASK-168` — leave empty until then._
