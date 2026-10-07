---
id: TASK-109
parent: STORY-038
feature: FEATURE-019
status: todo
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-518 (Birko.Framework)'
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

- [ ] `Program.cs` registers the framework's stores (SQLite when `KoboldLair:Data` selects SQLite, else in-memory) with the same DB path
- [ ] DraCode's two store files and its `AddOAuthServerStores` helper are deleted
- [ ] Existing OAuth data in `koboldlair.db` still loads (same table layout, or a migration)
- [ ] The OAuth / service-account tests stay green

## Out of scope

- The framework change itself (Birko TASK-518)

## Human test plan

N/A — covered by the existing auth tests.

## Implementation plan
