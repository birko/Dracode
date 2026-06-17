---
id: FEATURE-019
created: 2026-05-31
---

# OAuth/OIDC authentication and per-caller identity — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Use a recognised sign-in provider for identity, with GitHub as the first one and others addable later | approved | DraCode hosts its own OAuth server (IdP) and federates upstream to GitHub for human login | 2026-06-17 | human | TASK-030, TASK-033 |
| D2 | Validate caller identity in front of the HTTP and live-connection surfaces | approved | JWT validation middleware in front of `/api/v1/*`, `/dragon`, `/wyvern`, `/kobold` | 2026-06-17 | human | TASK-032 |
| D3 | Map each caller to a stored user record and give projects an owner | approved | `User` table + `projects.ownerId`; per-user scoping default | 2026-06-17 | human | TASK-034 |
| D4 | Issue scoped service credentials for non-human callers such as bots and CI runners | approved | `client_credentials` confidential clients carrying scopes; registration UX | 2026-06-17 | human | TASK-035 |
| D5 | Let a trusted local daemon skip sign-in; require it only for remote, network-exposed servers | approved | Loopback (`127.0.0.1`) daemon bind trusts the OS user; any non-loopback bind enforces JWT | 2026-06-17 | human | TASK-032, TASK-049 |
| D6 | Migrate existing projects to a legacy owner and keep the old shared-token system one release as a fallback | approved | `projects.json` rows migrate to `ownerId = "legacy"`; legacy auth deprecated, removed next release | 2026-06-17 | human | TASK-034, TASK-036 |
| D7 | Default project visibility to the caller's own work, with an opt-in view-all for operators | approved | `GET /api/v1/projects` defaults to caller's own; admins opt into `?scope=all` | 2026-06-17 | human | TASK-034 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-017 behaviour, migration, and risks.
- 2026-06-17 — reconciled with task tree: D1–D7 proposed → approved (ratified by STORY-017 execution, already in-progress); `→ Tasks` wired to TASK-030…036; feature back-link added to those tasks. TASK-030 (host OAuth server) and TASK-031 (SQLite stores) already shipped.
