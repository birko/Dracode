---
id: TASK-043
parent: STORY-016
feature: FEATURE-018
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-042, TASK-034]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Project / spec / feature / task / plan REST endpoints

## Context

The CRUD bulk of the facade. Thin minimal-API handlers wrapping the **same services the Dragon council tools already call** (project/spec/feature/task services, `SqlPlanRepository`) — no duplicated logic. Per-caller scoping uses `ICurrentUser` + `ownerId` (TASK-034).

## Acceptance criteria

- [ ] Projects: `GET/POST /projects`, `GET/DELETE /projects/{id}` (list filtered by owner; `?scope=all` for admins)
- [ ] Spec: `GET/PUT /projects/{id}/specification`
- [ ] Features: `GET/POST /projects/{id}/features`, `DELETE …/features/{featureId}`
- [ ] Tasks: `GET /projects/{id}/tasks`, `GET /tasks/{id}`, `POST /tasks/{id}/retry`, `POST /tasks/{id}/priority`
- [ ] Plans: `GET /projects/{id}/plans`, `GET /plans/{id}` (with step progress)
- [ ] All wrap existing services; ownership enforced; tests cover scoping + each verb

## Out of scope

- Runs/SSE (TASK-044/045); agents/cost (TASK-046)

## Human test plan

- [ ] As user A, `curl` create a project and list — see it; as user B, list — don't see A's; admin `?scope=all` sees both

## Implementation plan

_Populated by `/tasks plan TASK-043` — leave empty until then._
