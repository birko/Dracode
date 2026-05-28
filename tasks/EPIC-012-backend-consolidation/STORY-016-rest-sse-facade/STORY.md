---
id: STORY-016
parent: EPIC-012
status: planned
created: 2026-05-28
---

# REST + SSE facade for non-streaming clients

## User story

As a Discord bot / CI script / curl user, I want HTTP REST endpoints (with SSE for streaming progress) so I can interact with KoboldLair without keeping a long-lived WebSocket open. WebSocket remains for interactive streaming surfaces (Dragon chat, `/kobold` runs); REST covers everything else.

## Behaviour

- All endpoints under `/api/v1/...`, JSON in/out, `camelCase` body fields (matches existing JS serialization conventions)
- OpenAPI spec auto-generated (Swashbuckle / NSwag) at `/api/v1/openapi.json` + Swagger UI at `/api/v1/docs`
- All endpoints require auth via STORY-017's middleware; local daemon mode bypasses

### Resources

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/v1/projects` | List projects (filtered by caller identity in OAuth mode) |
| POST | `/api/v1/projects` | Create project |
| GET | `/api/v1/projects/{id}` | Full project state |
| DELETE | `/api/v1/projects/{id}` | Cancel + delete |
| GET | `/api/v1/projects/{id}/specification` | Load spec |
| PUT | `/api/v1/projects/{id}/specification` | Update spec |
| GET | `/api/v1/projects/{id}/features` | List features |
| POST | `/api/v1/projects/{id}/features` | Add feature |
| DELETE | `/api/v1/projects/{id}/features/{featureId}` | Delete feature |
| GET | `/api/v1/projects/{id}/tasks` | List tasks with status |
| GET | `/api/v1/tasks/{id}` | Single task detail |
| POST | `/api/v1/tasks/{id}/retry` | Reset to Unassigned |
| POST | `/api/v1/tasks/{id}/priority` | Set priority |
| GET | `/api/v1/projects/{id}/plans` | List Kobold plans |
| GET | `/api/v1/plans/{id}` | Plan with step progress |
| POST | `/api/v1/runs` | Start ad-hoc Kobold run (returns `runId`, then stream via SSE) |
| GET | `/api/v1/runs/{id}` | Run status |
| GET | `/api/v1/runs/{id}/events` | **SSE stream** of tool calls, reflections, completion |
| GET | `/api/v1/agents/active` | Currently running Drakes / Kobolds |
| GET | `/api/v1/cost-report?period=daily\|monthly` | Cost tracking |

### Implementation notes

- REST handlers are thin wrappers over the existing services in `DraCode.KoboldLair.Server/Services` (`DragonService`, `WyvernService`, `KoboldFactory`, `SqlPlanRepository`, etc.) — no duplicated logic
- SSE channel reuses the same internal event bus the WS endpoints publish to — one source of events, two transports

## Risks

- API shape churn: once a Python SDK + Discord bot depend on `/api/v1/...`, breaking changes hurt. Pin the v1 contract carefully; expose unstable bits behind `/api/v1/experimental/...` if needed.
- SSE timeouts behind reverse proxies (nginx, cloud LBs) — document `proxy_read_timeout` requirements.
