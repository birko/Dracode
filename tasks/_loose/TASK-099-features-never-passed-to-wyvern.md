---
id: TASK-099
parent: null
feature: null
status: todo
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

- [ ] Wyvern analysis receives the project's specification with its features; `Ready`/`Draft` features appear in the prompt and move to `AssignedToWyvern` with their feature branches
- [ ] `analysis.processedFeatures` lists the features that were analysed
- [ ] Regression test: analysing a project whose spec has `Ready` features assigns them; proven to fail before the fix
- [ ] Full suite green

## Out of scope

- Empty/unparseable Wyvern replies — TASK-098

## Human test plan

- [ ] On the dev server, approve a feature → after analysis `specification.features.json` shows it `AssignedToWyvern` and `git branch` in the project lists `feature/<id>-…`

## Implementation plan

_Populated by `/tasks plan TASK-099` — leave empty until then._
