---
id: TASK-045
parent: STORY-016
feature: FEATURE-018
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-044, TASK-037]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# SSE stream: GET /api/v1/runs/{id}/events

## Context

Streams tool-calls / reflections / completion for a run over **native ASP.NET Core SSE** (write `text/event-stream` from a minimal-API handler), subscribing to TASK-037's run-event source. Do **not** stand up Birko's HttpListener-based `SseServer`; optionally reuse its `SseEvent` for wire formatting. `?token=` auth is already handled by TASK-032's middleware (EventSource can't set headers).

## Acceptance criteria

- [ ] `GET /api/v1/runs/{id}/events` returns `text/event-stream`, subscribing to the run's event source
- [ ] Emits ordered events (tool-call, reflection, step, completion) until run end, then closes
- [ ] Auth via `?token=` (browser `EventSource`) works through the existing middleware
- [ ] Late subscribers get subsequent events; client disconnect cleans up the subscription
- [ ] Tests: SSE handler emits the expected event frames for a scripted run

## Out of scope

- Reverse-proxy timeout tuning (documented as a risk, not code)
- The run engine itself (TASK-044) and event source (TASK-037)

## Human test plan

- [ ] `curl -N "/api/v1/runs/{id}/events?token=…"` (or a browser `EventSource`) → see live event frames stream until completion
- [ ] Document `proxy_read_timeout` requirement and verify the stream survives behind a local nginx with it set

## Implementation plan

_Populated by `/tasks plan TASK-045` — leave empty until then._
