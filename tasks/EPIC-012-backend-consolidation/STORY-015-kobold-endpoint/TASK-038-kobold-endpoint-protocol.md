---
id: TASK-038
parent: STORY-015
feature: FEATURE-017
status: blocked
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-037]
blocks: [TASK-039, TASK-040]
pr: null
github-issue: null
jira-key: null
---

# /kobold WebSocket endpoint + message protocol

## Context

`app.MapWebSocket("/kobold", ...)` (Birko.Communication.WebSocket extension, same as `/dragon`). Parses the initial-message mode payload, subscribes the WS connection to TASK-037's run-event source, and emits the `kobold_*` message shapes. Mode handlers (ad-hoc/project) are TASK-039/040.

## Acceptance criteria

- [ ] `/kobold` endpoint mapped; auth applied consistently with the other WS endpoints (resolve Birko WS middleware vs JWT-bearer — STORY-015 open question)
- [ ] Initial payload parsed; dispatches to ad-hoc vs project handler by `mode`
- [ ] Emits `kobold_run_started` ({runId, worktree, mode}), then streams `kobold_stream`, `kobold_tool_call`, `kobold_reflect`, `kobold_complete`, `error` from the run-event source
- [ ] Client disconnect mid-run cleans up the subscription without killing the Kobold
- [ ] Tests: protocol round-trip with a fake run-event producer

## Out of scope

- Ad-hoc git/worktree mechanics (TASK-039); project plan/worktree reuse (TASK-040)

## Human test plan

- [ ] Connect a WS client (e.g. `websocat`) to `/kobold`, send a project-mode payload, observe the ordered `kobold_*` message stream through to `kobold_complete`

## Implementation plan

_Populated by `/tasks plan TASK-038` — leave empty until then._
