---
id: FEATURE-019
created: 2026-05-31
---

# OAuth/OIDC authentication and per-caller identity — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Use a recognised sign-in provider for identity, with GitHub as the first one and others addable later | proposed | — | — | — | — |
| D2 | Validate caller identity in front of the HTTP and live-connection surfaces | proposed | — | — | — | — |
| D3 | Map each caller to a stored user record and give projects an owner | proposed | — | — | — | — |
| D4 | Issue scoped service credentials for non-human callers such as bots and CI runners | proposed | — | — | — | — |
| D5 | Let a trusted local daemon skip sign-in; require it only for remote, network-exposed servers | proposed | — | — | — | — |
| D6 | Migrate existing projects to a legacy owner and keep the old shared-token system one release as a fallback | proposed | — | — | — | — |
| D7 | Default project visibility to the caller's own work, with an opt-in view-all for operators | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-017 behaviour, migration, and risks.
