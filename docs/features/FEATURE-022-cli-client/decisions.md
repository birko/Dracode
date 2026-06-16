---
id: FEATURE-022
created: 2026-05-31
---

# DraCode.KoboldLair.Cli — single-file binary CLI client — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Ship a single-file `koboldlair` binary for Windows, Linux, and macOS (Intel + Apple Silicon) | proposed | One artifact per platform, no install steps for developers | — | — | — |
| D2 | Provide a full verb set: `do`, `run`, `chat`, `analyze`, `merge`, `status`, `stop`, `login`, `keys`, `projects` | proposed | Covers ad-hoc runs, project work, planning chat, and account/key management from the terminal | — | — | — |
| D3 | `do`/`run` spawn an ephemeral server child process; `chat`/`analyze` discover or spawn a daemon; `--server` forces a remote daemon with OAuth | proposed | Zero-setup local use while still supporting shared/remote daemons | — | — | — |
| D4 | Interactive UX: ASCII logo and status header for `chat` only, streaming token-by-token output, color-coded progress | proposed | Friendly interactive experience without polluting scriptable command output | — | — | — |
| D5 | First release distributes `dotnet publish` artifacts on a GitHub release; package managers deferred | proposed | Fastest path to a usable download; ecosystem packaging can follow | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-019 behaviour (verbs, daemon interaction, UX, distribution).
