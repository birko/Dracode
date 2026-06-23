---
id: TASK-039
parent: STORY-015
feature: FEATURE-017
status: review
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

- [x] cwd validated; if not a git repo, `git init` + initial commit of existing files (via `GitService`) — `AdHocRunModeHandler.EnsureRepositoryAsync`: `IsRepositoryAsync` → `InitRepositoryAsync` → seed `.koboldlair/.gitignore` (so the snapshot has content and worktrees stay untracked in the parent tree) → `StageAllAsync` → `CommitChangesAsync`; guards against an existing repo with no commits
- [x] Worktree created under `<cwd>/.koboldlair/.worktrees/r-<runId>/` — `CreateBranchAsync(kobold/adhoc-<runId>)` then `CreateWorktreeAsync` at `WorktreePath(cwd, runId)`
- [x] Kobold spawned in the worktree via `KoboldFactory`; agent type from payload or auto-detected — `CreateKobold(provider, agentType, { WorkingDirectory = worktree })`; `ResolveAgentType` = explicit payload override → `DetectAgentType` (dominant source-file extension across the cwd, ignoring `.git`/`.koboldlair`/`node_modules`/`bin`/`obj`/`dist`) → `coding` fallback. **Open question resolved:** auto-detect from cwd files, with explicit `agentType` override
- [x] Run tracked off-registry by `runId`; completion returns `{ status, worktree, runId }` — in-memory `ConcurrentDictionary<Guid, AdHocRun>` (no `projects.json` entry — **open question resolved: off-registry**); `worktree`+`runId` ride the `kobold_run_started` frame, `status` the terminal `kobold_complete`/`error` frame (TASK-038 protocol)
- [x] Cleanup policy for stale ad-hoc worktrees after N days — `CleanupStaleWorktreesAsync` prunes `r-*` worktrees in the cwd older than `WorktreeRetention` (7 days); called at the start of every run for that cwd (restart-safe; no background timer needed)
- [~] Tests: adhoc run against a temp dir produces a worktree + commit — the **live Kobold edit→commit** needs a real LLM provider, so it lives in the human test plan (cf. TASK-040). The deterministic scaffolding (auto git-init + initial snapshot + branch + worktree) is unit-tested against real git with no LLM (`AdHocRunModeHandlerTests`: `EnsureRepository_inits_and_snapshots_existing_files_*`, `Adhoc_scaffolding_produces_a_worktree_and_a_commit`), plus the pure helpers and the cleanup policy. Full suite 114/114 green

## Out of scope

- Widening `PathHelper` for out-of-sandbox cwds (TASK-041)
- The `koboldlair merge` CLI verb (EPIC-013)

## Human test plan

- [ ] Point ad-hoc mode at a non-git temp folder with a file → confirm `.git/` + worktree created, Kobold edits land in the worktree, original cwd untouched until merge

## Implementation plan

**Bespoke handler, not a Drake caller.** Unlike project mode (TASK-040, which reuses `Drake.ExecuteTaskAsync`), ad-hoc has no project/task-file/feature-branch state, and the worktree path is the story-mandated `<cwd>/.koboldlair/.worktrees/r-<runId>/` (not Drake's `.worktrees/{branch}`). So `AdHocRunModeHandler` is standalone, wired only from `GitService` + `KoboldFactory` + `ProviderConfigurationService` + `KoboldRunEventSource` (all already singletons — DI registration in `Program.cs` unchanged, the stub just gained a real ctor).

**RunId / subscribe-before-start (same contract as TASK-040):** the endpoint owns the `runId` and subscribes to `KoboldRunEventSource` *before* `StartAsync`; the handler calls `kobold.AssignRunId(runId)` before `StartWorkingWithPlanAsync`, so the transport sees telemetry under the id it is watching with no drop-before-subscribe race. The Kobold emits its own terminal `RunCompletedEvent`/`RunErrorEvent` + `CompleteRun` (existing TASK-037 wiring); after it returns, the handler stages + commits the worktree (`Kobold-<agentType>` author) so the caller can `git merge kobold/adhoc-<runId>`.

**Streaming note:** ad-hoc runs without an implementation plan (`StartWorkingWithPlanAsync(planService: null)`), so the per-tool-call/step frames — which the enhanced/plan path publishes — aren't emitted; ad-hoc streams `kobold_run_started` → `kobold_complete`. Plan-backed ad-hoc streaming can be layered on later without protocol changes.

**Done:**
1. `AdHocRunModeHandler` replacing the TASK-038 stub: validate → prune stale worktrees → `EnsureRepositoryAsync` → resolve agent type → branch + worktree → spawn Kobold → background run + commit; off-registry `_runs` tracking + `GetRun`.
2. `KoboldRunRequest` gains `Cwd`/`Prompt` aliases (documented field names) resolved as `Cwd ?? WorkingDirectory` / `Prompt ?? Task`.
3. Pure/testable seams: `ValidateRequest`, `ResolveAgentType`, `DetectAgentTypeFromExtensions`, `BranchName`/`WorktreeRoot`/`WorktreePath`, static `EnsureRepositoryAsync(git, cwd)` + `CleanupStaleWorktreesAsync(...)`.
4. `AdHocRunModeHandlerTests` (17): helpers + real-git scaffolding + cleanup policy.

**Out of scope kept:** `PathHelper` widening for out-of-sandbox cwds (TASK-041) — ad-hoc sets the Kobold's `WorkingDirectory` to the worktree, so file ops sandbox to it; the `koboldlair merge` verb (EPIC-013).
