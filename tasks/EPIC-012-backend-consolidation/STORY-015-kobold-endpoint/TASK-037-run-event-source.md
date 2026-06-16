---
id: TASK-037
parent: STORY-015
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: []
blocks: [TASK-038, TASK-044, TASK-045]
pr: null
github-issue: null
jira-key: null
---

# Internal per-run event source (Kobold tool-loop event sink)

## Context

⭐ **Shared foundation for STORY-015 (WS) and STORY-016 (SSE)** — build once, two transports subscribe. Kobold today runs **headless**: `StartWorkingWithPlanAsync` / `StartWorkingWithPlanEnhancedAsync` return a `List<Message>` only at completion; the sole progress callback is `OnEscalation`. There is no per-tool-call / per-reflection / per-step hook. This task adds an internal per-run event stream that the tool loop publishes to (the `reflect` and `update_plan_step` tools already produce the structured data).

## Acceptance criteria

- [ ] A per-run event abstraction (cf. `Birko.EventBus`) keyed by `runId`, with subscribe/publish
- [ ] Kobold tool loop publishes: tool-call (start/result), reflection, plan-step update, completion, error
- [ ] Publication is **non-blocking** — if no subscriber, events are dropped/buffered without stalling execution (hot path safety)
- [ ] Subscribers can attach mid-run and receive subsequent events
- [ ] Tests: a fake subscriber receives the expected event sequence for a scripted Kobold run

## Out of scope

- The `/kobold` WS transport (TASK-038) and SSE transport (TASK-045) — they consume this
- Persisting events (in-memory stream is sufficient for v1)

## Human test plan

- [ ] N/A — fully covered by automated tests (transports get their own manual tests)

## Implementation plan

_Populated by `/tasks plan TASK-037` — leave empty until then._
