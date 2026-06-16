---
id: TASK-068
parent: STORY-020
feature: FEATURE-023
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-067, TASK-044]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Discord commands + thread streaming

## Context

Implement `/do`, `/chat`, `/projects`, `/status` against the REST/run API. `/do` creates a thread for run output; `/chat` starts a Dragon session (thread per session). Stream incrementally by editing the bot's reply, **rate-limit aware** (batch flush every ~500ms or 200 chars). Handle Discord's 15-min interaction-token timeout via deferred response + thread messages.

## Acceptance criteria

- [ ] `/do <prompt>` starts an ad-hoc run, opens a thread, streams output (batched edits)
- [ ] `/chat` starts a Dragon session in a thread; subsequent messages continue it
- [ ] `/projects` lists projects visible to the service account; `/status` shows daemon + active agents
- [ ] Streaming batches chunks to respect Discord rate limits; long runs use deferred response + thread messages (no 15-min timeout failures)
- [ ] Tests for command routing + the chunk-batching logic

## Out of scope

- `/login` user linkage + session store (TASK-069)

## Human test plan

- [ ] `/do "add a README"` → bot opens a thread and streams tool calls without hitting rate limits; a long run (>15 min) still completes via thread messages

## Implementation plan

_Populated by `/tasks plan TASK-068` — leave empty until then._
