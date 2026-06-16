---
id: FEATURE-020
created: 2026-05-31
---

# Daemon mode for KoboldLair.Server — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run the server as a per-user background daemon with start, stop, and status commands | proposed | — | — | — | — |
| D2 | Record the running daemon's details in a state file in the user's home folder | proposed | — | — | — | — |
| D3 | Use a lock file so only one daemon runs per user; the loser connects to the winner | proposed | — | — | — | — |
| D4 | Have clients auto-discover a running daemon and spawn one on demand if none is found | proposed | — | — | — | — |
| D5 | Let a client bypass discovery and talk to a given address directly | proposed | — | — | — | — |
| D6 | Drain in-flight work on graceful shutdown, then clean up the state file | proposed | — | — | — | — |
| D7 | Handle stale state from an unclean shutdown via a liveness check and a forced cleanup path | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-018 behaviour, discovery, and risks.
