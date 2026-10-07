---
id: TASK-093
parent: null
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: [FIELD-010]
pr: null
github-issue: null
jira-key: null
---

# A deleted project's Wyvern stays registered by name and is reused by a new project with that name

## Context

Found 2026-10-03 during TASK-091's live check. `WyvernFactory` keeps its Wyverns keyed by **project name**, and deleting
a project does not remove its Wyvern. A new project created with the same name then got the old Wyvern: the log read
"Wyvern not found for project … re-assigning" → "already has status WyrmAssigned, skipping Wyvern assignment" →
"Assigned wyvern", and the tasks it wrote carried the **deleted** project's id (`ProjectId` set at creation, TASK-091).
In one such run the analysis also came back with **0 areas / 0 tasks** and the project was marked Analyzed anyway —
whether that was the stale Wyvern or model variance is not established; a zero-area analysis being accepted as success
is worth checking on its own.

## Acceptance criteria

- [x] Deleting a project (REST and Dragon) removes its Wyvern (and any Drakes) from the factories — `ProjectService.ReleaseAgents`, called by `DeleteProjectAsync` and REST `DELETE /projects/{id}`
- [x] A new project with a deleted project's name gets a fresh Wyvern carrying its own project id — covered by a test (`ProjectDeletionTests`; a stale Wyvern left any other way is replaced too)
- [x] Decide what an analysis with zero areas/tasks should do (fail and retry, or mark Failed with a reason) instead of reaching Analyzed; implement and test it — done by TASK-098 (an empty or task-less reply fails the analysis; regression test `WyvernUnusableReplyTests`)

## Out of scope

- Deleting a project's task rows and files (TASK-074)

## Human test plan

N/A — factory state and the zero-area rule are unit-testable.

## Implementation plan

Drafted 2026-10-07 from the code as of `9207ea1`.

**Where it goes wrong.** `ProjectService.DeleteProjectAsync` (Dragon's `delete_project`) removes folder, task and plan
rows and the project row, but never touches `WyvernFactory` / `DrakeFactory`. REST `DELETE /api/v1/projects/{id}`
(`ResourceEndpoints.cs:86`) only calls `repo.DeleteAsync`. `RetryAnalysis` and `ResetProjectAsync` already do the
release (Drakes by project id, Wyvern by name). `AssignWyvernAsync` / `AnalyzeProjectAsync` look the Wyvern up by
name and never check that its `ProjectId` is this project's.

1. `ProjectService`: add `ReleaseAgents(Project)` — `RemoveAllDrakesForProject(project.Id)` + `RemoveWyvern(project.Name)`,
   returning what it removed; call it from `DeleteProjectAsync` (adds a note) and reuse it in `RetryAnalysis` /
   `ResetProjectAsync`, which do the same by hand.
2. REST `DELETE /projects/{id}`: call `projects.ReleaseAgents(project)` before `repo.DeleteAsync`. Switching REST to
   `DeleteProjectAsync` (rows/files policy) stays with TASK-074.
3. Guard: wherever `ProjectService` takes a Wyvern by name (`AssignWyvernAsync`, `AnalyzeProjectAsync`), a Wyvern whose
   `ProjectId` is not this project's is stale — remove it and create a fresh one. Covers a project row deleted by any
   other path.
4. Tests (`ProjectDeletionTests`, with an ollama provider config so a Wyvern/Drake can be built without a request):
   delete via `DeleteProjectAsync` removes the Wyvern and Drakes; a new project with the same name gets a Wyvern whose
   `ProjectId` is the new id (both through deletion and through the stale-Wyvern guard). Prove each fails without its fix.

## Close notes

- Closed 2026-10-07. Review gate (inline): correctness found that `ReleaseAgents` removed a Wyvern matched by
  *sanitized* name, which could be another live project's (`My App` vs `my-app`); it now removes only a Wyvern whose
  `ProjectId` is this project's — test `Deleting_leaves_another_projects_Wyvern_whose_name_sanitizes_the_same`, proven
  to fail without the guard. Conventions: no agent prompt touched; no new pattern. Security: the REST delete keeps its
  ownership and permission checks; no new surface.
- No REST-level test: in `ResourceEndpointsTests` the test host resolved the Wyvern provider `pi-zai`, which the
  temp setup does not define, so a Wyvern cannot be built there. REST calls the service-tested `ReleaseAgents`.
