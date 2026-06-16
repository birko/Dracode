---
id: FEATURE-001
created: 2026-05-31
---

# Token storage & auth providers — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Store secrets in the OS-native secure vault, auto-selected per platform (Windows/Mac/Linux) | proposed | Removes plain-text exposure of API keys and tokens | — | — | TASK-001 |
| D2 | Silently migrate existing plain-text secrets into the vault on first use | proposed | No disruption — users don't re-enter anything | — | — | TASK-001 |
| D3 | Add Google and GitHub sign-in alongside the existing login | proposed | Lets people use accounts they already have | — | — | TASK-002 |
| D4 | Make account creation policy and role mapping configurable (open vs invite-only) | proposed | Lets operators control who gets in and at what access level | — | — | TASK-002 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from EPIC-001 success criteria and the acceptance themes of TASK-001 (encrypted storage) and TASK-002 (OAuth integration).
