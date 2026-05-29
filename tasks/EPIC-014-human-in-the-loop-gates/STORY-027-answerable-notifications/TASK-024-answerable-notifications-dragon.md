---
id: TASK-024
parent: STORY-027
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-05-29
depends-on: [TASK-020, TASK-022]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Answerable notifications + Dragon actionable prompt round-trip

## Context

Pending decisions (TASK-020) and `ask_human` questions (TASK-021) park tasks, but a human can only resolve them once Dragon can *show and collect* the answer. Extend the notification model with a response schema and render an actionable prompt in the Dragon client, wiring the submitted answer back through the resolution callback (which TASK-022 delivers into agent context).

## Acceptance criteria

- [ ] `ProjectNotification` gains optional `requiresResponse` (bool) + `responseSchema` (choice options / free-text shape).
- [ ] A `requiresResponse` notification is emitted when a task parks / an agent asks, pushed live via the existing `OnNotification` → WebSocket path.
- [ ] Dragon client renders it as an actionable prompt (choice buttons / text input), not just a readable line.
- [ ] Submitting the answer calls the resolution callback → unparks the task (verified end-to-end with TASK-022).
- [ ] A tool to list + answer pending decisions explicitly (extend `NotificationsTool` or a new tool) for missed live prompts.
- [ ] Idempotent resolution: answering an already-resolved decision is a no-op with a clear message.
- [ ] Offline persistence: decision raised while human disconnected is replayed on reconnect (consistent with existing notification persistence).

## Out of scope

- Policy editing UI — TASK-023 covers the policy model.
- Non-Dragon clients (EPIC-013 territory).

## Human test plan

- [ ] Trigger a parked decision while Dragon is connected; confirm an actionable prompt appears and answering it unparks and resumes the task.
- [ ] Disconnect Dragon, trigger a decision, reconnect; confirm the prompt is replayed.
- [ ] Answer the same decision twice; confirm the second attempt is a clean no-op.

## Implementation plan

_Populated by `/tasks plan TASK-024` — leave empty until then._
