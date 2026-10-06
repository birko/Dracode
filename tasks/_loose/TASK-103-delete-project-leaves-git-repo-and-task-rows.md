---
id: TASK-103
parent: null
feature: null
status: todo
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

- [ ] Deleting a project removes its folder even when it contains a git repository (clear read-only attributes before deleting, or equivalent)
- [ ] Deleting a project removes its task rows (and any other per-project rows that are working state — check plans and the planning context; usage/cost records are history and stay)
- [ ] When file deletion fails, the tool's reply says so (what is left and where) instead of reporting success
- [ ] Regression tests: a project folder with a read-only file is removed; task rows for the project are gone after deletion; proven to fail before the fix
- [ ] Full suite green

## Out of scope

- External (imported) projects — their source folder must never be deleted; confirm the existing guard still holds

## Human test plan

- [ ] In Dragon, delete a project that has commits → its folder is gone from `ProjectsPath` and `select count(*) from tasks where ProjectId=…` is 0

## Implementation plan

_Populated by `/tasks plan TASK-103` — leave empty until then._
