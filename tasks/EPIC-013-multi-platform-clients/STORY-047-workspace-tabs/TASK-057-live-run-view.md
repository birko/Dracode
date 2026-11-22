---
id: TASK-057
parent: STORY-047
feature: FEATURE-026
status: done
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

- [x] Selecting a run opens its view: the plan steps as a checklist with progress, and live tool calls, reflections and completion
- [x] Opening a run that has already finished shows how it ended
- [x] A dropped stream reconnects without losing or repeating steps
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it with the stream stubbed, red before the change; the suite stays green
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)

## Out of scope

- The run list (TASK-155)
- The SSE endpoint itself (TASK-045, done)

## Human test plan

- [x] Start a run from the CLI (`koboldlair do ...`), open it from the Runs tab, and watch it live to completion
- [x] Open a run that finished before the page loaded: it shows its final state at once

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Run view** (`views/run-view.ts`, `kobold-run-view`): selecting a run in the Runs tab (click, or Enter/Space on the
  row) opens it in the tab's place; "← All runs" comes back. It shows what the run is for, its status, the plan's
  steps as a checklist with a progress bar (`GET /api/v1/plans/{taskId}` for a run with a task; an ad-hoc run has no
  plan), and an activity feed: tool calls with their input, results, reflections (progress, confidence, decision,
  blockers), step changes and how the run ended. The feed keeps the last 300 lines.
- **Stream:** Birko.Web.Core's `SseClient` on `GET /api/v1/runs/{id}/events` (`?token=` from the auth store; retries
  after 1, 3, 10 s), through `createSseClient`, which now takes those options. The server replays nothing and closes
  after a run's last frame, so: a run already ended at opening is shown from `GET /api/v1/runs/{id}` with no stream;
  the stream is closed on the last frame (else the client would reconnect to an ended run); after a reconnect, frames
  already shown are skipped by their sequence number (one counter per run across connections) and the status and plan
  are read again for the gap, merged so nothing moves back (a step keeps the furthest of the two states; an ended run
  stays ended). Tool inputs are re-serialized so the server's unicode escapes read as characters.
- **Tests:** e2e `run-view.spec.ts`, 6 tests (all red on the old client): a live run's checklist, feed and ending, with
  no reconnect after it; a finished run without a stream; a dropped stream resumed without repeats and with the plan
  re-read; back to the list; an ad-hoc run (no plan) and escaping; phone width. Suite 119/119.
- **Human test** (throwaway Testing server on a scratch copy, JWT on, LAN model): `waver2`'s retried task's pipeline run
  opened from the Runs tab as it appeared and was followed over the real stream to its end — its file reads, a
  command, a reflection, both plan steps checked off, "The run completed." — with one stream request and no reconnect.
  Reopened after a reload it showed its end at once, without a stream. An ad-hoc run started over `/kobold` (no CLI
  client yet, TASK-050/051) had finished before the list showed it, so it opened at its end. Phone width, dark: no
  horizontal scroll; no console errors. The test showed tool inputs full of unicode escapes; fixed.
