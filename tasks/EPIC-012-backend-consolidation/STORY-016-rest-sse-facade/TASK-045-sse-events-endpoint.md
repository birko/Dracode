---
id: TASK-045
parent: STORY-016
feature: FEATURE-018
status: done
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

- [x] `GET /api/v1/runs/{id}/events` returns `text/event-stream`, subscribing to the run's event source — `RunsEndpoints`, native minimal-API handler (no Birko `SseServer`), frames are the `/kobold` wire messages
- [x] Emits ordered events (tool-call, reflection, step, completion) until run end, then closes — also a heartbeat comment every 15 s on an idle stream, and a run that already ended gets its closing frame
- [x] Auth via `?token=` (browser `EventSource`) works through the existing middleware — tests open the stream with `?token=` only; no token → 401
- [x] Late subscribers get subsequent events; client disconnect cleans up the subscription — `KoboldRunEventSource.SubscriberCount` returns to the registry's own after a disconnect
- [x] Tests: SSE handler emits the expected event frames for a scripted run — `RunEventsSseTests` (5); proven: dropping the end-of-run break, the already-ended check or the heartbeat fails 3 of them, leaking the subscription fails the disconnect test

## Out of scope

- Reverse-proxy timeout tuning (documented as a risk, not code)
- The run engine itself (TASK-044) and event source (TASK-037)

## Human test plan

- [x] `curl -N "/api/v1/runs/{id}/events?token=…"` (or a browser `EventSource`) → see live event frames stream until completion — 2026-10-07, dev server with JWT on, ad-hoc run ("create hello.txt"): `kobold_run_started`, 4× `kobold_tool_call`, `kobold_complete` (Done); curl exited when the stream closed
- [ ] Document `proxy_read_timeout` requirement and verify the stream survives behind a local nginx with it set — documented in `docs/setup-guides/RUN_EVENTS_SSE.md`; ⚠ NOT MET — nginx is not installed on this machine; the verification is split to TASK-107

## Implementation plan

Done 2026-10-07.

1. `RunsEndpoints`: `GET /runs/{id:guid}/events` — ownership as `GET /runs/{id}`; subscribe before the headers go out;
   write `id`/`event`/`data` frames (`KoboldWireMessage` JSON) until `RunCompletedEvent`/`RunErrorEvent`; heartbeat comment
   every `SseHeartbeat` (15 s) on an idle stream, re-checking the run registry so a run that ended before the subscription
   (no replay) gets its closing frame; `X-Accel-Buffering: no`.
2. `KoboldWireMessage.JsonOptions` shared by both transports; `RunStartedEvent` maps to `kobold_run_started`.
3. `KoboldRunEventSource.SubscriberCount` (observes the disconnect cleanup).
4. `docs/setup-guides/RUN_EVENTS_SSE.md`: usage, frames, proxy settings.
5. Tests: `RunEventsSseTests`.

## Close notes

- Closed 2026-10-07. Suite 251/251. The nginx check is split to TASK-107 (needs a machine with nginx).
