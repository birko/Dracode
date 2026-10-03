---
id: TASK-091
parent: null
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: [FIELD-008]
pr: null
github-issue: null
jira-key: null
---

# Tasks of a new project never reach the database, so the REST task endpoints can't see them

## Context

Found 2026-10-03 during TASK-086's live run. Project `signoff-throwaway` (created and approved on a running server)
was analyzed by Wyvern into one task (`f6fa9bd2-…`, in `tasks/scripting-tasks.json`), and Drake/Kobold executed it
to Done with a commit. Yet `GET /api/v1/projects/{id}/tasks` returned `[]`, `POST /api/v1/tasks/{id}/retry`
returned 404, and the database `tasks` table held **0** rows for the project. `DrakeFactory` gives its
`TaskTracker` a `Repository`, so tasks are expected to persist; somewhere between Wyvern writing the task file and
Drake tracking it, the database copy is not written (or is written under another project id). The REST surface
(TASK-043) and anything reading tasks from the database (Warden tools, progress views) therefore miss them.

## Acceptance criteria

- [x] Find where task rows should be written and why none are for a freshly analyzed project (check the project id written, the TaskTracker/Repository wiring on the Wyvern and Drake paths) — three causes: Wyvern's tracker had no repository and ProjectService never passed `projectId` to `CreateWyvern`; Drake loads tasks from the file, which never adds rows, so its updates hit missing rows (swallowed); and `EntityMapper.UpdateEntity` overwrote `ProjectId` with null from file-loaded records
- [x] After analysis, every task in the project's task files has a database row with the project id; status changes are reflected — new `TaskTracker.EnsureInRepositoryAsync` (idempotent): Wyvern awaits it after writing a task file; Drake runs it in the background after loading one (back-fills older projects); updates keep the stored project id
- [x] `GET /api/v1/projects/{id}/tasks` lists them and `POST /tasks/{id}/retry` finds them — covered by a test — `TaskTrackerRepositoryBackfillTests` (4): file-loaded tasks get rows, idempotent, an update without a project id keeps it (fails without the mapper fix), no-repository no-op; disabling the add line fails 2 of them. Full suite 184/184
- [x] Live: a project analyzed on the dev server shows its tasks over REST — 2026-10-03: `GET /projects/{id}/tasks` returned the task with the right projectId (status Done after Drake ran it); `GET /tasks/{id}` 200. Found on the way: TASK-093

## Out of scope

- Changing the task-file format

## Human test plan

N/A — criterion 4 is the live check.

## Implementation plan

Not drafted separately — the causes and fixes are recorded on the criteria above.