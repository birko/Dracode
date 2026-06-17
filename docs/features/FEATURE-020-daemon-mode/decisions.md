---
id: FEATURE-020
created: 2026-05-31
---

# Daemon mode for KoboldLair.Server — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run the server as a per-user background daemon with start, stop, and status commands | approved | Daemon lifecycle flags on KoboldLair.Server | 2026-06-17 | human | TASK-047 |
| D2 | Record the running daemon's details in a state file in the user's home folder | approved | `daemon.json` state file (dynamic port, absolute ProjectsPath) in the user's home | 2026-06-17 | human | TASK-048 |
| D3 | Use a lock file so only one daemon runs per user; the loser connects to the winner | approved | Single-daemon-per-user via lock; loser attaches to the running instance | 2026-06-17 | human | TASK-048 |
| D4 | Have clients auto-discover a running daemon and spawn one on demand if none is found | approved | Client discovery helper reads the state file, spawns on demand | 2026-06-17 | human | TASK-049 |
| D5 | Let a client bypass discovery and talk to a given address directly | approved | Explicit-address bypass for the discovery helper | 2026-06-17 | human | TASK-049 |
| D6 | Drain in-flight work on graceful shutdown, then clean up the state file | approved | Graceful drain on shutdown, then state-file cleanup | 2026-06-17 | human | TASK-047 |
| D7 | Handle stale state from an unclean shutdown via a liveness check and a forced cleanup path | approved | Liveness check against stale `daemon.json`; forced-cleanup path | 2026-06-17 | human | TASK-048 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-018 behaviour, discovery, and risks.
- 2026-06-17 — decide: D1–D7 proposed → approved (ratifying the STORY-018 design; work not yet started); `→ Tasks` wired to TASK-047…049 and feature back-link added to those tasks. Phase → building (0/3 done).
