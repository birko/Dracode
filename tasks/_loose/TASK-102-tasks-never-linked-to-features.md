---
id: TASK-102
parent: null
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-06
depends-on: [TASK-099]
blocks: []
findings: [FIELD-016]
pr: null
github-issue: null
jira-key: null
---

# Wyvern tasks are never linked to their features, so no work lands on feature branches; re-analysis of a new feature yields no task for it

## Context

Found 2026-10-06 during TASK-099's live check on `drake-switch-check`. After TASK-099, Wyvern receives the features, puts
them in the prompt, assigns them (`AssignedToWyvern`, persisted) and creates their branches
(`feature/21ea737c-greeting-script`, `feature/d710eb88-usage-readme`, `feature/cd126c51-uppercase-flag`), and
`analysis.processedFeatures` lists all three. But:

- **No task carries a `featureId`** (task files and the `tasks` table: all null). `Wyvern.LinkTasksToFeatures`
  (`Wyvern.cs:~343`) — the only code that sets `WyvernTask.FeatureId` and `Feature.TaskIds` — has no callers. Without a
  feature id, Drake and `/kobold` project mode commit to `main`, never to the feature branch (TASK-040's "commit lands on the
  task's feature branch" cannot happen; observed: cli-1 → `8675142` on main, doc-1 → `b2040c6` on main). Its matching rule
  (feature name contained in the task name/description) is also weak — check it against real Wyvern output.
- **The new feature got no task.** Adding "Uppercase flag" to an analysed project and re-analysing produced 2 tasks: `cli-1`
  (re-emitted, matched to the existing Done task) and `docs-1` (a near-duplicate of the Done `doc-1`) — nothing for
  `--upper`. Re-analysis of an incrementally changed spec needs to produce tasks for the new feature and not duplicate done
  ones.
- A `verification-fixes` area file appeared with no tasks.

## Acceptance criteria

- [x] Every task Wyvern creates for a feature carries that feature's id (and the feature lists its task ids), on first analysis and on re-analysis
- [x] Re-analysing after a feature is added yields at least one task for the new feature and does not duplicate tasks that are already Done
- [x] A task with a feature id is committed on that feature's branch by both Drake and `/kobold` project mode
- [x] Regression tests for the linking and the incremental re-analysis; proven to fail before the fix
- [x] Full suite green
- [x] Live: add a feature to an analysed project → its task appears linked to the feature, and running it commits on `feature/<id>-…`

## Out of scope

- Passing features to Wyvern and assigning/branching them — done in TASK-099

## Human test plan

- [x] On the dev server, add a feature to an analysed project, run its task (Drake or `/kobold`), then `git log feature/<id>-…` shows the commit and `main` does not

## Implementation plan

Decided 2026-10-06 (owner): schema `featureId` with an exact-name fallback (substring heuristic dropped); a new feature that receives no task stays Ready and is retried; duplicates skipped by id in any area, or by name only against Done tasks.

1. Prompt names each new feature `(id: …)`; the reply schema gains `featureId`; the system prompt requires a task per new feature and forbids re-emitting listed Existing Tasks (overriding the always-README rule).
2. Re-analysis prompt lists `## Existing Tasks` (`[id] name — status`) from `tasks/*-tasks.md` (`Wyvern.LoadExistingTaskSummaries`).
3. `Wyvern.LinkTasksToFeatures(analysis, features)` (static) replaces the unused substring matcher; only features that received a task are assigned, the rest stay Ready (logged); the sidecar is persisted once.
4. `CreateTasksAsync` sets `TaskRecord.FeatureId`, dedups across all area files, writes no file for an area with only duplicates (`AreasWithoutNewTasks`, not counted as pending), and merges into the existing task-file map case-insensitively; `ProjectService` always passes the existing map.
5. Drake resolves the branch from the task's `FeatureId` and the features sidecar (`Drake.ResolveFeatureBranch`) — it no longer needs a Wyvern instance (`SetWyvern` has no callers) — so both Drake and `/kobold` project mode commit on the feature branch. Commit-message feature info prefers `FeatureId`.
6. Live: add a feature to an analysed project, run its task via `/kobold` and via Drake, check `git log feature/…`.

## Progress log

- 2026-10-06 — implemented per the plan. `WyvernFeatureLinkingTests` (5): featureId reaches the task record and the feature's `TaskIds` and the prompt shows `id: f1`; a name echo links, an unknown id does not; a feature without a task stays Ready; a re-analysis lists existing tasks and adds only the new feature's task (id dedup + Done-name dedup), keeping earlier areas; `Drake.ResolveFeatureBranch`. Four mutations (no FeatureId copy, no linking, no Done-name dedup, no Existing Tasks section) each turn a test red. TASK-099's assignment test now sends a `featureId` (a feature is assigned only when it receives a task). Full suite: 221 passed.
- 2026-10-06 — live (dev server, `branch-check`, with TASK-101's logger and Birko Framework TASK-516): first analysis → `core-1` linked to "Greeting script" (feature `TaskIds: ["core-1"]`, AssignedToWyvern), branch `feature/f30d0758-greeting-script`; README/review tasks unlinked. `/kobold` ran core-1 → commit `c050d03` on `feature/f30d0758-greeting-script`, `main` untouched, worktree removed. Added "Uppercase flag" → re-analysis added only `upper-1`/`upper-2`, both linked, no duplicates, `core-1` still Done, branch `feature/940cdc24-uppercase-flag`. Drake on → commit `8219974` (upper-1) on `feature/940cdc24-uppercase-flag`. This also shows TASK-040's "lands on the task's feature branch".
- 2026-10-06 — the live run's first Wyvern log lines (now visible via TASK-101) showed "Task [upper-1] has invalid dependencies: core-1. These will be removed.": with re-analysis emitting only new work, `ValidateAndFixTaskDependencies` stripped dependencies on existing tasks. It now also accepts ids from the project's task files; the re-analysis test asserts `(depends on: doc-1)` on the new task (red before). Full suite: 221 passed.
