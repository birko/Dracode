---
id: TASK-065
parent: STORY-022
feature: FEATURE-025
status: blocked
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-064, TASK-045]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Python SDK run streaming (SSE + polling fallback)

## Context

`run.stream()` async-iterates a run's events via `httpx-sse` against TASK-045's SSE endpoint, with a sync-polling fallback (`GET /runs/{id}`) for environments where SSE is blocked by proxies.

## Acceptance criteria

- [ ] `async for event in run.stream():` yields typed events from the SSE endpoint
- [ ] Token passed via `?token=` (matches server expectation)
- [ ] Sync-polling fallback when SSE is unavailable, surfaced via the same iterator contract where feasible
- [ ] Reconnect on transient drop; clean termination at run completion
- [ ] Tests: SSE stream parsing + fallback path (mocked)

## Out of scope

- The client base (TASK-064); publish (TASK-066)

## Human test plan

- [ ] Start a run via the SDK, `async for event in run.stream()` → prints live events to completion; simulate SSE blocked → fallback still reports progress

## Implementation plan

_Populated by `/tasks plan TASK-065` — leave empty until then._
