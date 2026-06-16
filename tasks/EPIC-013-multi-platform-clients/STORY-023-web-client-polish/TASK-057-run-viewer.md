---
id: TASK-057
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-045]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Web run viewer for external /kobold runs

## Context

A "Recent runs" panel that surfaces ad-hoc `/kobold` runs started outside the web UI (e.g. by the CLI) and lets the user watch the live SSE stream (TASK-045's `/api/v1/runs/{id}/events`). Builds on the existing `api-client.ts`.

## Acceptance criteria

- [ ] "Recent runs" panel lists runs (incl. CLI-started ones), with status
- [ ] Selecting a run opens a viewer that subscribes to the SSE event stream and renders tool-calls / reflections / completion live
- [ ] `EventSource` auth via `?token=` works (server-side already supported)
- [ ] Reconnect/late-attach handled gracefully
- [ ] Tests where supported

## Out of scope

- The SSE endpoint itself (TASK-045)
- Header affordances / auth UI (TASK-055/056)

## Human test plan

- [ ] Start a run from the CLI (`koboldlair do ...`), then open the web UI "Recent runs" → see that run and watch its event stream live to completion

## Implementation plan

_Populated by `/tasks plan TASK-057` — leave empty until then._
