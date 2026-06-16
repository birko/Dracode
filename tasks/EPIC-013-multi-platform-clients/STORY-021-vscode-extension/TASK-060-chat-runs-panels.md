---
id: TASK-060
parent: STORY-021
feature: FEATURE-024
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-059, TASK-038]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# VSCode Dragon chat panel + Runs panel

## Context

Wire the two side-bar panels to the backend. "Chat" drives a Dragon session over `/dragon` (WS); "Runs" lists active + recent Kobolds and streams a selected run's events (over `/kobold` WS or the SSE endpoint).

## Acceptance criteria

- [ ] Chat panel: interactive Dragon session with streamed responses (WS)
- [ ] Runs panel: lists active + recent runs; selecting one streams its events live
- [ ] Connection respects the configured server / daemon (TASK-061 supplies discovery + auth)
- [ ] Graceful handling of disconnect / reconnect
- [ ] Tests where the extension test harness supports them

## Out of scope

- Auth + daemon discovery (TASK-061)
- Inline diff + Run on Selection (TASK-062)

## Human test plan

- [ ] Open the Chat panel, send a message → see streamed Dragon reply; start a run → it appears in the Runs panel and streams to completion

## Implementation plan

_Populated by `/tasks plan TASK-060` — leave empty until then._
