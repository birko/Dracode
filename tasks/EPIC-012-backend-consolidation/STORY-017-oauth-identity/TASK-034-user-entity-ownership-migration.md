---
id: TASK-034
parent: STORY-017
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-032]
blocks: [TASK-043]
pr: null
github-issue: null
jira-key: null
---

# User entity, project ownership, and projects.json migration

## Context

Per-caller identity needs a DraCode `User` record (keyed by stable `sub`) and an `ownerId` on each project. Existing `projects.json` rows migrate to a system `ownerId = "legacy"` sentinel; future creates require a real owner. `ICurrentUser` (from TASK-032) supplies the caller identity for scoping.

## Acceptance criteria

- [ ] `User` entity + `users` table (Birko.Data.SQL, same DB)
- [ ] `projects` gain `ownerId` foreign key (model + persistence + view model + mapper)
- [ ] One-time migration: existing `projects.json` rows → `ownerId = "legacy"`; new creates require a real owner from `ICurrentUser`
- [ ] Per-caller scoping default: callers see only their own projects; `?scope=all` opt-in for admins
- [ ] Tests: migration assigns legacy sentinel; scoping filters by owner; admin override returns all

## Out of scope

- The REST endpoints that surface scoping (TASK-043 consumes this)
- Service-account ownership semantics beyond `sub = "service:…"` (TASK-035)

## Human test plan

- [ ] Run against an existing `projects.json` with pre-auth projects → all show `ownerId=legacy`; create a new project as a logged-in user → it carries that user's id; a second user does not see the first user's projects

## Implementation plan

_Populated by `/tasks plan TASK-034` — leave empty until then._
