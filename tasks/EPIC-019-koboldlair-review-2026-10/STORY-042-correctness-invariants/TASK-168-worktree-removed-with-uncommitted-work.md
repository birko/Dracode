---
id: TASK-168
parent: STORY-042
feature: null
status: done
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

- [x] A run that ends without committing (cancelled, failed, or not Done for any reason) keeps its worktree, so a resumed run continues on its files
- [x] Worktrees are still removed after a successful commit, and an abandoned one is still cleaned up by some path (say which — e.g. startup prune, task cancellation)
- [x] A test pins the shutdown case: cancelled run → worktree still there; red on the current code

## Out of scope

- The parked-task case (TASK-020)

## Human test plan

- [x] Start a project task, stop the server mid-run, restart it: the task resumes and the files written before the stop are still in its worktree
  - **Passed 2026-11-22:** run by the owner (Ctrl+C in their terminal); see Notes

## Implementation plan

1. Pull the after-run decision into `Drake.ReleaseWorktreeAsync(task, worktreePath)`: keep the worktree when its commit
   failed, when its task is parked, when another run of the feature still uses it (a per-Drake count, since a
   worktree is per feature branch, not per task), or when `git status --porcelain` shows uncommitted work; otherwise
   remove it. Call it on the normal path and on the exception path.
2. `GitService.HasUncommittedChangesAsync`.
3. Tests against a real repo and worktree (`DrakeWorktreeTests`), red on the old rule; human test with a real graceful
   stop.

## Notes

- **Fix** (`Drake.ReleaseWorktreeAsync`, `Drake.AcquireWorktree`, `GitService.HasUncommittedChangesAsync`): after a run
  (and in its exception path, so a failed run keeps what it wrote) the worktree is removed only when nothing would be
  lost. It is kept while its commit failed, while its task waits on a decision, while another run of the same feature
  still uses it (worktrees are per feature branch; Drake counts the runs using each), and while it holds uncommitted
  work, or git cannot tell. A run acquires the worktree once its Kobold is summoned.
- **Which path cleans up a kept worktree:** the feature's next run reuses it (`CreateWorktreeAsync` reuses a valid
  worktree) and the run that commits and leaves it clean removes it. Nothing else removes a worktree holding work —
  its files are the only copy. The startup prune still removes only invalid worktrees.
- **Tests:** `DrakeWorktreeTests` (4, real git repo and worktree): a run that ended without committing keeps its
  worktree and files and the next run reuses them (the shutdown case); a failed run keeps what it wrote; a committed
  run that leaves it clean removes it; a worktree another run of the feature uses is kept, then removed when that run is
  done. The three keep tests are red on the old rule. Suites .NET 542/542, e2e 137/137 (one cold-start failure on the
  first run after the client host started, as on TASK-186; the next two runs green).
- **Human test** (throwaway Testing server on a scratch copy of `projects160` with its stored paths moved to the copy,
  waver2's `impl-1` given a feature on `feature/w2-greet`, model slowed to 3 calls a minute): run by the owner with
  Ctrl+C in their terminal mid-run. The shutdown logged "Keeping worktree …" and the worktree kept `workspace/waver.js`
  (uncommitted). After the restart the task resumed in the same worktree, finished, committed `b3765ce` to
  `feature/w2-greet` (`workspace/waver.js`), and the worktree was removed. A run without the stop (6.7 s) committed and
  removed its worktree too. Not tested by a person: a failed run (automated only).
- **Found while testing, not changed:** the scratch databases store absolute paths, so a copied project folder still
  points at the original — a throwaway copy must have its stored paths moved (the human-test recipe's `rehome.py`).
