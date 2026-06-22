---
id: STORY-016
parent: EPIC-012
status: in-progress
created: 2026-05-28
depends-on: [STORY-017]
---

# REST + SSE facade for non-streaming clients

## User story

As a Discord bot / CI script / curl user, I want HTTP REST endpoints (with SSE for streaming progress) so I can interact with KoboldLair without keeping a long-lived WebSocket open. WebSocket remains for interactive streaming surfaces (Dragon chat, `/kobold` runs); REST covers everything else.

## Behaviour

- All endpoints under `/api/v1/...`, JSON in/out, `camelCase` body fields (matches existing JS serialization conventions)
- OpenAPI spec auto-generated at `/api/v1/openapi.json` + a docs UI at `/api/v1/docs`
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

### Hosting model (corrected)

The server is an **ASP.NET Core `WebApplication`** (it already uses `app.MapWebSocket` for `/dragon`, `/wyvern` and minimal-API for `/auth/*`). The REST facade must therefore be **ASP.NET Core minimal-API routes**, *not* a separate host:

- ⚠️ **Do not use `Birko.Communication.REST.Server`** — its `RestServer` is `HttpListener`-based (standalone hosting), which would run a parallel server outside the ASP.NET Core pipeline and miss STORY-017's auth middleware + OpenAPI. Map `/api/v1/...` as `app.MapGet/MapPost/...` instead, so they sit behind `AddBirkoSecurity`.
- ⚠️ **`Birko.Communication.SSE` is its own self-contained SSE framework** (own `SseServer`/`SseContext`/`SseRequestDelegate`, HttpListener-style — not an ASP.NET Core pipeline integration). For `/api/v1/runs/{id}/events`, prefer **native ASP.NET Core SSE** (write `text/event-stream` from a minimal-API handler); optionally reuse Birko's `SseEvent` for wire formatting. Don't stand up a second HttpListener.

### Building blocks (use these)

| Concern | Use |
|---|---|
| Auth on all routes + `?token=` for SSE | Inherited from STORY-017 (`AddBirkoSecurity` — its JWT middleware already reads `?token=` from the query string, which is exactly what `EventSource` needs since it can't set headers) |
| OpenAPI spec + docs UI | **.NET 10 built-in `Microsoft.AspNetCore.OpenApi`** (`AddOpenApi()` / `MapOpenApi()`). Swashbuckle/NSwag only if a richer Swagger UI is wanted; built-in is the default for .NET 10. |
| Backing logic | Thin wrappers over **existing** services/repositories — `DragonService`, `WyrmService`, `WyvernProcessingService`, `KoboldFactory`, `SqlPlanRepository`, and the project/spec/feature/task services the Dragon tools already call. No duplicated logic. |

### Implementation notes

- REST handlers wrap the **same service methods the Dragon council tools already invoke** (e.g. `manage_specification`, `set_task_priority`, `retry_failed_task`) — REST and the Dragon WS surface become two transports over one set of services.
  - (Correction: the original draft referenced a `WyvernService`; no such class exists — Wyvern is `WyvernProcessingService` + `WyvernVerificationService`. Project/task CRUD lives in the `DraCode.KoboldLair` library services, not a Wyvern service.)
- **Run-events source:** `/api/v1/runs/{id}/events` needs an internal event stream of tool-calls / reflections / completion. Today streaming exists only for **Dragon chat** (`DragonService` + `DragonRequestQueue`, the `dragon_stream` path); a general per-run event source for **Kobold** runs is partly net-new. Decide whether to introduce a small internal event bus (cf. `Birko.EventBus`, noted in the EPIC) that both the future `/kobold` WS endpoint (STORY-015) and this SSE endpoint subscribe to — **one source of events, two transports**.

## Suggested task breakdown

1. **Minimal-API `/api/v1` skeleton** — route group + `AddOpenApi`/`MapOpenApi` + docs UI, behind STORY-017's auth (and daemon bypass).
2. **Project / spec / feature / task / plan read+write endpoints** — wrap existing services; enforce per-caller scoping (`ICurrentUser` from STORY-017).
3. **Runs endpoints** (`POST /runs`, `GET /runs/{id}`) — start ad-hoc Kobold run, return `runId`.
4. **Internal run-event source** — event stream for tool-calls/reflections/completion, shared with STORY-015's `/kobold`.
5. **SSE endpoint** `GET /runs/{id}/events` — native ASP.NET Core `text/event-stream`, subscribing to #4; `?token=` auth already handled.
6. **`agents/active` + `cost-report`** read endpoints.

## Dependencies

- **Hard depends on STORY-017** — the "all endpoints require auth" guarantee uses its `AddBirkoSecurity` middleware (and `ICurrentUser` for project scoping). Alternatively, an early cut of this story can ship behind the **daemon loopback bypass** only, deferring per-caller auth until 017 lands.
- Shares the internal run-event source with **STORY-015** (`/kobold` endpoint).

## Risks

- API shape churn: once a Python SDK + Discord bot depend on `/api/v1/...`, breaking changes hurt. Pin the v1 contract carefully; expose unstable bits behind `/api/v1/experimental/...` if needed.
- SSE timeouts behind reverse proxies (nginx, cloud LBs) — document `proxy_read_timeout` requirements.
