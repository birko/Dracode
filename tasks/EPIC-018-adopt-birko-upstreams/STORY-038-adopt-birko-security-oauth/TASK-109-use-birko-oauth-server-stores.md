---
id: TASK-109
parent: STORY-038
feature: FEATURE-019
status: done
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
blocked: null
pr: null
github-issue: null
jira-key: null
---

# Use the framework OAuth server stores; delete SqliteOAuthStores and InMemoryOAuthStores

## Context

`DraCode.KoboldLair.Server/Auth/SqliteOAuthStores.cs` (276 lines) and `InMemoryOAuthStores.cs` (84) implement the Birko OAuth
server's store interfaces with nothing DraCode-specific. Birko TASK-518 moves them into the framework.

Framework side: Birko TASK-518 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [x] `Program.cs` registers the framework's stores (SQLite when `KoboldLair:Data` selects SQLite, else in-memory) with the same DB path
- [x] DraCode's two store files are deleted; `AddOAuthServerStores` stays as a thin helper that registers the framework stores
- [x] Existing OAuth data in `koboldlair.db` still loads (same table layout, or a migration)
- [x] The OAuth / service-account tests stay green

## Out of scope

- The framework change itself (Birko TASK-518)

## Human test plan

N/A — covered by the existing auth tests.

## Implementation plan

Done 2026-10-19 in `acb4307`.

## Close notes

- Closed 2026-10-19. Suite 253/253. `AddOAuthServerStores` registers Birko.Security.OAuth.Server.SqLite / .InMemory (same table
  and column names). An existing `oauth_refresh_tokens` table gains `ClaimsJson` at startup (`EnsureColumns`).
