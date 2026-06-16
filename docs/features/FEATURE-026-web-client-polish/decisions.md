---
id: FEATURE-026
created: 2026-05-31
---

# DraCode.KoboldLair.Client — UI polish reclaimed from EPIC-003 — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Drag-and-drop tab reordering in the project workspace view | proposed | Reclaimed polish item from the retired EPIC-003 backlog | — | — | — |
| D2 | Saveable named workspace layouts (open panels, splitter sizes, active tabs) persisted to the user profile on the server | proposed | Lets users keep and restore their preferred workspace arrangement | — | — | — |
| D3 | Daemon status indicator in the header (green/red, click for details) | proposed | New affordance exposed by the multi-platform rework | — | — | — |
| D4 | Project switcher in the header that preserves chat scroll position across switches | proposed | Fast multi-session-aware switching without losing context | — | — | — |
| D5 | OAuth login button replacing the token-paste field | proposed | Aligns web sign-in with the GitHub OAuth flow used across clients | — | — | — |
| D6 | Service-account key management UI under user settings → API Keys | proposed | Mirrors the CLI key verbs so keys can be managed from the web | — | — | — |
| D7 | Recent-runs panel to view and stream ad-hoc runs started outside the web UI | proposed | Surfaces CLI/other-client runs in the web client | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-023 behaviour (reclaimed EPIC-003 items + new multi-platform affordances).
