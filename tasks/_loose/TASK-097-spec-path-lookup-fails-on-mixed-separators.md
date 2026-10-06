---
id: TASK-097
parent: null
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-04
depends-on: []
blocks: [TASK-095]
findings: [FIELD-012]
pr: null
github-issue: null
jira-key: null
---

# Approved features never reach Wyvern when ProjectsPath uses forward slashes (spec-path lookup compares mixed separators)

## Context

Found 2026-10-04 during TASK-095's live check. On the dev machine `appsettings.local.json` sets
`ProjectsPath: "C:/Source/DraCode-Projects"`, so a new project's stored specification path is
`C:/Source/DraCode-Projects\drake-switch-check\specification.md` (forward slash from config, backslashes from `Path.Combine`).
`SqlProjectRepository.GetBySpecificationPath` (`SqlProjectRepository.cs:167-175`) compares the stored string against
`Path.GetFullPath(specPath)`, which on Windows normalises to all backslashes — the strings never match. So
`ProjectService.MarkSpecificationModified` (`ProjectService.cs:729`) logs "No project found" at Debug and returns: Sage's
`process_features` approve/promote (`ProcessFeaturesTool.cs:223`) and `manage_specification` updates never move an
Analyzed/InProgress/Completed project to `SpecificationModified`, and Wyvern never analyses the new features.

Measured: project `drake-switch-check` — spec approved with no features → Wyvern `Tasks: 0`; two features then added and
approved (status Ready in `specification.features.json`) → project stayed `Analyzed` for 8+ minutes, no "Specification
modified" log line, 0 tasks. The JSON-file `ProjectRepository` has the same comparison shape — check it too.

## Acceptance criteria

- [x] Spec-path lookup matches regardless of separator style or a relative/absolute form (both sides normalised the same way) in every `IProjectRepository` implementation
- [x] Regression test: a project registered under a forward-slash `ProjectsPath` is found by `GetBySpecificationPath` with the backslash form (and vice versa), and `MarkSpecificationModified` moves an Analyzed project to `SpecificationModified`; proven to fail before the fix
- [ ] Live: approving a new feature on an analyzed project on the dev server makes Wyvern re-analyse it and create its tasks — ⚠ PARTLY MET: re-analysis now triggers (verified live 2026-10-04); "create its tasks" NOT MET — split to TASK-098
- [x] Full suite green

## Out of scope

- Normalising the stored paths of existing projects (the lookup fix makes them match as they are)
- Other path comparisons in the codebase — this task fixes the spec-path lookup that gates re-analysis
- Deferred to TASK-098 — Wyvern returning 0 tasks for the re-analysed project (empty reply → silent empty analysis)
- Deferred to TASK-099 — approved features never passed to Wyvern

## Human test plan

- [ ] In Dragon, add and approve a feature on an analyzed project → within ~60 s Wyvern logs a re-analysis and the feature's tasks appear — ⚠ re-analysis observed 2026-10-04; tasks appearing is TASK-098's live check

## Implementation plan

1. Normalise the **stored** side of the comparison the same way as the caller's path: `SqlProjectRepository.GetBySpecificationPath` compares `Path.GetFullPath` of both (new `NormalizeSpecPath`, empty stays empty); the JSON `ProjectRepository` runs the stored path through its existing `ResolvePath`.
2. Regression tests (`SpecificationPathLookupTests`): both repositories find a mixed-separator path by its backslash and forward-slash forms; `MarkSpecificationModified` moves an Analyzed project to `SpecificationModified` — written first, red before the fix.
3. Live: on the dev server, re-trigger the already-approved features of `drake-switch-check` (stuck at Analyzed, 0 tasks) and watch Wyvern re-analyse and create tasks. Run on a build that also carries TASK-095, so the project's Drake-off switch keeps the background Drake away (TASK-095 continues its live check from there).

## Progress log

- 2026-10-04 — `SpecificationPathLookupTests` (3) written first and red (the first draft passed vacuously through `?.`, tightened to assert non-null); fixed both lookups; green. Full suite: 195 passed.
- 2026-10-04 — live (dev server, build with TASK-095): re-triggering `drake-switch-check`'s approved features now logs `📝 Specification modified … will be reprocessed` and Wyvern re-analyses (`[Wyvern] REANALYZE`) — the lookup fix works. The "and create its tasks" half could not be shown: the re-analysis returned 0 tasks for a separate reason, filed as TASK-098 (and TASK-099).
- 2026-10-06 — closed on delivered scope (the lookup fix, verified live); the task-creation half of the live criterion moved to TASK-098, recorded unticked above.
