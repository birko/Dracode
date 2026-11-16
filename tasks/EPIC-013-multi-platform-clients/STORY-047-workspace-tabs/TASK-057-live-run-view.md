---
id: TASK-057
parent: STORY-047
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-06-11
depends-on: [TASK-045, TASK-155]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Runs tab: live run view

> Rewritten 2026-11-16 by `/feature decompose FEATURE-026`, and moved from STORY-023 to STORY-047. It was a
> standalone "Recent runs" panel. The run list is now the workspace's Runs tab (TASK-155), and this task is the
> view that opens a run from that list.

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces.
Data comes only from `/api/v1` (owner-scoped).

Selecting a run in the Runs tab opens its live view: the plan's steps with progress, plus the run's tool calls,
reflections and completion as they happen (D7, D20).

**Build on the framework:** use Birko.Web.Core's `SseClient` (`?token=`, reconnect) on
`GET /api/v1/runs/{id}/events`. Its frames are the same as the `/kobold` WebSocket's (guide
`docs/setup-guides/RUN_EVENTS_SSE.md`). The token comes from the Shell auth store. No local EventSource wrapper.

For project runs, the plan steps come from `GET /api/v1/plans/{id}`.

Decisions: FEATURE-026 D7, D20.

## Acceptance criteria

- [ ] Selecting a run opens its view: the plan steps as a checklist with progress, and live tool calls, reflections and completion
- [ ] Opening a run that has already finished shows how it ended
- [ ] A dropped stream reconnects without losing or repeating steps
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it with the stream stubbed, red before the change; the suite stays green
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)

## Out of scope

- The run list (TASK-155)
- The SSE endpoint itself (TASK-045, done)

## Human test plan

- [ ] Start a run from the CLI (`koboldlair do ...`), open it from the Runs tab, and watch it live to completion
- [ ] Open a run that finished before the page loaded: it shows its final state at once

## Implementation plan

_Populated by `/tasks plan TASK-057` — leave empty until then._
