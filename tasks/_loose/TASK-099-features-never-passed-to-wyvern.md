---
id: TASK-099
parent: null
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-04
depends-on: []
blocks: []
findings: [FIELD-014]
pr: null
github-issue: null
jira-key: null
---

# Approved features are never given to Wyvern — analysis sees only specification.md and features stay Ready

## Context

Found 2026-10-04 while tracing TASK-095's live check. `ProjectService.AnalyzeProjectAsync` (`ProjectService.cs:350`, and
`:364` for pending areas) calls `wyvern.AnalyzeProjectAsync(null, wyrmRecommendation)` — the `Specification` (with its
features from `specification.features.json`) is never passed. Inside `Wyvern.AnalyzeProjectAsync` (`Wyvern.cs:~539-570`)
`_specification` is therefore null on a freshly assigned Wyvern (always the case after a restart — "Wyvern not found …
re-assigning"), so `newFeatures` is empty: the features' names/descriptions never enter the prompt, `AssignFeaturesAsync`
never runs (no `AssignedToWyvern`, no `feature/{id}` branches), and `analysis.processedFeatures` stays `[]`. The only method
that sets `_specification`, `Wyvern.GetNewFeaturesAsync`, has no callers. Measured on `drake-switch-check`: two `Ready`
features, two analyses, both features still `Ready`, `processedFeatures: []`.

## Acceptance criteria

- [x] Wyvern analysis receives the project's specification with its features; `Ready`/`Draft` features appear in the prompt and move to `AssignedToWyvern` with their feature branches — live 2026-10-06
- [x] `analysis.processedFeatures` lists the features that were analysed
- [x] Regression test: analysing a project whose spec has `Ready` features assigns them; proven to fail before the fix
- [x] Full suite green

## Out of scope

- Empty/unparseable Wyvern replies — TASK-098

## Human test plan

- [x] On the dev server, approve a feature → after analysis `specification.features.json` shows it `AssignedToWyvern` and `git branch` in the project lists `feature/<id>-…` — observed 2026-10-06: three features `AssignedToWyvern` with `gitBranch`, three `feature/…` branches

## Implementation plan

1. `ProjectService.LoadSpecificationForAnalysisAsync(project)` (static) builds the `Specification` from `project.Paths.Specification` and loads the features sidecar beside it; both analysis calls (full and pending-areas) pass it to `Wyvern.AnalyzeProjectAsync`.
2. `Wyvern.AnalyzeProjectAsync` assigns the Ready/Draft features only after the analysis succeeds (a failed one leaves them Ready for the next attempt) and persists the sidecar with `SpecificationService.PersistFeaturesAsync` — before, the status change was in memory only.
3. Tests with a recording `ILlmProvider`; live: add a feature to `drake-switch-check` and check status + feature branch.

## Progress log

- 2026-10-06 — `WyvernFeatureAssignmentTests` (3): the loader carries the features; a successful analysis puts the Ready feature in the prompt, persists it `AssignedToWyvern` and lists it in `processedFeatures` (red against the previous Wyvern — status was never persisted); a failed analysis leaves it Ready. Full suite: 202 passed. Feature branches need a git service, so they are left to the live check.
- 2026-10-06 — live (dev server, combined build): added and approved "Uppercase flag" on `drake-switch-check` → Wyvern re-analysis: all three features `AssignedToWyvern` and persisted with `gitBranch`, branches `feature/21ea737c-greeting-script`, `feature/d710eb88-usage-readme`, `feature/cd126c51-uppercase-flag` created, `processedFeatures` lists all three. Tasks were not linked to features and the new feature got no task — filed as TASK-102.
