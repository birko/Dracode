---
id: TASK-069
parent: STORY-020
feature: FEATURE-023
status: blocked
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-067, TASK-033]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Discord /login user linkage + thread→session store

## Context

MVP-blocking for audit: `/login` OAuth-links a Discord user to a KoboldLair user so commands run under their identity, not the bot's service account. Persist the Discord-user→KoboldLair-user mapping and thread→Dragon-session mapping in a local SQLite/Birko.Data store.

## Acceptance criteria

- [ ] `/login` runs the OAuth flow (STORY-017) and stores a Discord-user → KoboldLair-user link
- [ ] After linking, a user's commands execute under their KoboldLair identity (audit shows the real user, not the bot)
- [ ] Unlinked users fall back to the service account (or are prompted to `/login`, per decision)
- [ ] Thread → Dragon-session mapping persisted (SQLite/Birko.Data), survives bot restart
- [ ] Tests: link/lookup; session mapping persistence

## Out of scope

- Command behaviour/streaming (TASK-068)
- Server-side OAuth (STORY-017)

## Human test plan

- [ ] Run `/login`, complete OAuth → run `/do` → server-side audit attributes the run to the linked user; restart the bot → an existing thread still maps to its Dragon session

## Implementation plan

_Populated by `/tasks plan TASK-069` — leave empty until then._
