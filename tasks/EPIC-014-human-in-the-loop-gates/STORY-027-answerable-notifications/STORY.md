---
id: STORY-027
parent: EPIC-014
status: planned
created: 2026-05-29
---

# Answerable decisions surfaced in Dragon

## User story

As a human using Dragon, I want pending agent decisions to appear as actionable prompts I can answer in-chat, so that I don't have to poll `view_notifications` and the pipeline isn't silently waiting on me.

## Behaviour

- Extend `ProjectNotification` / `ProjectNotificationService` with an optional `requiresResponse` flag and a `responseSchema` (the choice options / free-text shape from the parked question).
- When a task parks (STORY-024) or an agent calls `ask_human` (STORY-025), a `requiresResponse` notification is emitted and pushed to the Dragon client in real time (existing `OnNotification` event + WebSocket path).
- Dragon renders it as an actionable prompt (choice buttons / input), not just a readable line; submitting feeds the answer back through the round-trip plumbing to unpark the task.
- A new Dragon/Warden tool (or extension of `NotificationsTool`) lets the human list and answer pending decisions explicitly, for the case where the live prompt was missed.
- Decisions that have been answered are marked resolved and removed from the pending set.
- Edge case: the same decision must not be answerable twice (idempotent resolution); a second answer is a no-op with a clear message.
- Edge case: if the human is offline when the decision is raised, it persists and is replayed on reconnect (consistent with existing notification persistence).
