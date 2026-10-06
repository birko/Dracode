---
id: TASK-103
parent: null
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-06
depends-on: []
blocks: []
findings: [FIELD-017]
pr: null
github-issue: null
jira-key: null
---

# delete_project reports success but leaves a git project's folder and the project's task rows behind

## Context

Found 2026-10-06 deleting the throwaway project `drake-switch-check` through Dragon (Warden `cancel_project` then
`delete_project`). Dragon answered "removed from the registry along with all its files", but:

- **The folder survived.** `DragonService.DeleteProjectFromRegistry` (`DragonService.cs:~1926-1951`) calls
  `Directory.Delete(folder, recursive: true)`; git object files are read-only on Windows, so it threw
  `System.UnauthorizedAccessException: Access to the path '05503e9f…' is denied`, logged "Failed to delete project files",
  and carried on — leaving `.git/` behind. Every KoboldLair project has a git repo (`ProjectService.CreateProjectFolderAsync`
  runs `git init`), so this hits any project that has had a commit. An earlier project with no commits deleted cleanly.
- **The project's rows in `tasks` were not deleted** (3 remained for project `8178a21b…`). Nothing removes a deleted
  project's tasks, so they linger in the table the REST task endpoints read.
- The tool's reply does not reflect either failure.

Cleaned up by hand this time (folder removed, task rows deleted with the server stopped).

## Acceptance criteria

- [x] Deleting a project removes its folder even when it contains a git repository (clear read-only attributes before deleting, or equivalent)
- [x] Deleting a project removes its task rows (and any other per-project rows that are working state — check plans and the planning context; usage/cost records are history and stay)
- [x] When file deletion fails, the tool's reply says so (what is left and where) instead of reporting success
- [x] Regression tests: a project folder with a read-only file is removed; task rows for the project are gone after deletion; proven to fail before the fix
- [x] Full suite green

## Out of scope

- External (imported) projects — their source folder must never be deleted; confirm the existing guard still holds

## Human test plan

- [x] In Dragon, delete a project that has commits → its folder is gone from `ProjectsPath` and `select count(*) from tasks where ProjectId=…` is 0 — 2026-10-06: `delete-check` with a commit (3 read-only git objects) deleted through Dragon; folder gone, registry empty, reply "deleted C:\Source\DraCode-Projects\delete-check; 0 task(s) removed; 0 plan(s) removed"

## Implementation plan

1. Move deletion into `ProjectService.DeleteProjectAsync(projectId, deleteFiles)` returning `ProjectDeletionResult(Deleted, Message)`; `DragonService` delegates to it (its inline `DeleteProjectFromRegistry` removed).
2. Folder: only the project's own folder under the projects path (from the spec path, else projects path + sanitised name) — an imported project's source outside it is never touched; read-only attributes cleared before `Directory.Delete`. A failure is reported in the message, not swallowed.
3. Rows: the project's tasks (`ITaskRepository`) and plans (`SqlPlanRepository`), both now optional `ProjectService` dependencies wired in `Program.cs`; usage/cost records stay.
4. `DeleteProjectTool` awaits the result and puts its message in the reply.

## Progress log

- 2026-10-06 — `ProjectDeletionTests` (3): read-only git object folder removed; only the deleted project's task and plan rows go; an imported source folder outside the projects path survives. Each guard proven by mutation (attribute clearing, row deletion, projects-path check each removed → its test fails). Full suite: 216 passed. Live check as in the Human test plan.
