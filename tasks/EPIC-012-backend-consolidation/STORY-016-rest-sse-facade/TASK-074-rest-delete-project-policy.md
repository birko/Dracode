---
id: TASK-074
parent: STORY-016
feature: FEATURE-018
status: todo
priority: P2
assignee: ai
created: 2026-06-28
depends-on: [TASK-043]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# REST DELETE /projects — match the Dragon delete policy + clean up files

## Context

`DELETE /api/v1/projects/{id}` (TASK-043) calls `IProjectRepository.DeleteAsync(id)` on a project in
**any** execution state and leaves the on-disk project folder behind. The Dragon `delete_project` tool
(`DeleteProjectTool`) is stricter: it only permits deleting **Cancelled** projects and offers a
`deleteFiles` cleanup. The two surfaces diverged on the most destructive operation — surfaced by the
TASK-043 code review. A REST caller can currently delete a Running project out from under live
Drakes/Kobolds and orphan its folder (`specification.md`, features sidecar, `tasks/`, `workspace/`,
`.worktrees/`).

## Acceptance criteria

- [ ] `DELETE /projects/{id}` refuses a non-`Cancelled` (Running/Paused/Suspended) project → 409 with a
      message pointing at cancel-first, matching `DeleteProjectTool`'s guard
- [ ] On a permitted delete, the on-disk project folder is removed (mirroring the tool's `deleteFiles`),
      not just the registry row
- [ ] Extract the shared guard + cleanup so `DeleteProjectTool` and the REST handler call one code path
      (same "can't drift" rationale as `SpecificationService`)
- [ ] Tests: delete Running → 409; cancel then delete → 204 + folder gone; ownership still enforced (404)

## Out of scope

- Cascade semantics for worktrees mid-run (covered by existing worktree cleanup)

## Human test plan

- [ ] `curl -XDELETE` a Running project → 409; cancel it, delete → 204 and confirm the folder is gone on disk

## Implementation plan

_Populated by `/tasks plan TASK-074` — leave empty until then._
