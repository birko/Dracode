---
id: TASK-102
parent: null
feature: null
status: todo
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

- [ ] Every task Wyvern creates for a feature carries that feature's id (and the feature lists its task ids), on first analysis and on re-analysis
- [ ] Re-analysing after a feature is added yields at least one task for the new feature and does not duplicate tasks that are already Done
- [ ] A task with a feature id is committed on that feature's branch by both Drake and `/kobold` project mode
- [ ] Regression tests for the linking and the incremental re-analysis; proven to fail before the fix
- [ ] Full suite green
- [ ] Live: add a feature to an analysed project → its task appears linked to the feature, and running it commits on `feature/<id>-…`

## Out of scope

- Passing features to Wyvern and assigning/branching them — done in TASK-099

## Human test plan

- [ ] On the dev server, add a feature to an analysed project, run its task (Drake or `/kobold`), then `git log feature/<id>-…` shows the commit and `main` does not

## Implementation plan

_Populated by `/tasks plan TASK-102` — leave empty until then._
