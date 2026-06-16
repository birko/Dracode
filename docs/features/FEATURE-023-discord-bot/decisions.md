---
id: FEATURE-023
created: 2026-05-31
---

# Discord bot client — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | New `DraCode.KoboldLair.Discord` project built on the Discord.Net library | proposed | Mature C# Discord library; consistent with the .NET stack | — | — | — |
| D2 | Offer slash commands `/do`, `/chat`, `/projects`, `/status`, `/login` | proposed | Covers ad-hoc runs, planning chat, project visibility, status, and identity linkage from inside Discord | — | — | — |
| D3 | Bot authenticates as a service account via a JWT supplied in configuration | proposed | Lets the bot operate against a remote daemon without per-user setup | — | — | — |
| D4 | `/login` links a Discord user to a real KoboldLair user so commands run under their identity | proposed | Audit trail and per-user attribution; treated as MVP-blocking | — | — | — |
| D5 | Stream output by editing the bot's reply with batched chunks (~500ms / 200 chars) and use threads for long runs | proposed | Stays within Discord rate limits and the 15-minute interaction timeout | — | — | — |
| D6 | Persist thread-to-session mapping in a local store; optional per-guild allowlist | proposed | Resumable sessions and the ability to restrict which servers may use the bot | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-020 behaviour (library, slash commands, auth, streaming, persistence, configuration).
