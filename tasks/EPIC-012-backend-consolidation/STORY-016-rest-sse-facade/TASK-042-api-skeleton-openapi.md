---
id: TASK-042
parent: STORY-016
feature: FEATURE-018
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-032]
blocks: [TASK-043, TASK-044, TASK-046]
pr: null
github-issue: null
jira-key: null
---

# /api/v1 minimal-API skeleton + OpenAPI

## Context

REST facade is **ASP.NET Core minimal-API routes** under `/api/v1` (not Birko's HttpListener-based `RestServer`), so they sit behind TASK-032's auth middleware. OpenAPI via the **.NET 10 built-in `Microsoft.AspNetCore.OpenApi`** (`AddOpenApi`/`MapOpenApi`), not Swashbuckle. JSON camelCase in/out.

## Acceptance criteria

- [ ] `/api/v1` route group established, behind `AddBirkoSecurity` auth (daemon bypass honoured)
- [ ] `AddOpenApi()` + `MapOpenApi()` → spec at `/api/v1/openapi.json`; docs UI at `/api/v1/docs`
- [ ] camelCase JSON serialization matches existing JS conventions
- [ ] A trivial endpoint (e.g. `GET /api/v1/agents/active` stub) returns through the pipeline with auth enforced
- [ ] Tests: 401 unauthenticated, 200 authenticated, OpenAPI doc generated

## Out of scope

- The resource endpoints themselves (TASK-043/044/045/046)

## Human test plan

- [ ] Open `/api/v1/docs` in a browser → Swagger/OpenAPI UI renders the registered routes

## Implementation plan

_Populated by `/tasks plan TASK-042` — leave empty until then._
