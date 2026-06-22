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
| D6 | Migrate existing projects to a legacy owner and keep the old shared-token system one release as a fallback | changed | Superseded by D8 — greenfield rebuild, no legacy data or consumers to preserve | 2026-06-20 | human | TASK-034, TASK-036 |
| D7 | Default project visibility to the caller's own work, with an opt-in view-all for operators | approved | `GET /api/v1/projects` defaults to caller's own; admins opt into `?scope=all` | 2026-06-17 | human | TASK-034 |
| D8 | Treat this as a from-scratch rebuild: no project migration and no "legacy" owner, and switch the live connections off the old shared-token sign-in immediately rather than keeping it a release | changed | No legacy data or consumers exist, so a migration/fallback window is dead weight; `/dragon` + `/wyvern` move to JWT now so sessions carry a real identity. Supersedes D6 | 2026-06-20 | human | TASK-034, TASK-036 |
| D9 | Identify a project's owner by the caller's stable sign-in id (`sub`) directly, not an internal numeric/Guid key | approved | The `sub` is what every token carries (humans, bots, local-dev); avoids a lookup and handles service accounts uniformly. Refines D3 | 2026-06-20 | human | TASK-034 |
| D10 | Enforce per-caller project visibility on the live Dragon surface now, not only on the future REST API | approved | Once Dragon carries a real identity, its project list must honour ownership immediately or the guarantee is visibly false; operators (`view-all`) and local-dev still see everything. Extends D7 | 2026-06-20 | human | TASK-034 |
| D11 | A federated GitHub login keeps the raw external `sub` (`github:{id}`) as the identity everywhere, rather than minting a DraCode-internal Guid and demoting the GitHub id to a mapping | approved | Stays consistent with the just-approved D9 (own by raw `sub`); the callback mints a DraCode JWT directly. The internal-Guid model is cleaner long-term but would reopen D9 the same week it shipped — deferred to a later refactor | 2026-06-22 | human | TASK-033 |
| D12 | Carry the string `sub` through the refresh-token store, not a Guid | approved | `/auth/refresh` re-emits the stored key as the next token's `sub`, so the store must hold the string `sub` to preserve `github:{id}` across refresh. Aligns the last Guid-keyed component with the D9/D11 string-`sub` model | 2026-06-22 | human | TASK-033 |
| D13 | Gate who may federate with a deny-all-by-default allowlist of GitHub numeric ids, separate from authentication | approved | GitHub OAuth proves *who* you are, not *whether you're allowed in*; without a gate, anyone with a GitHub account could mint a token and spend the LLM budget. Keyed on the stable numeric id (usernames are mutable); empty list ⇒ no one admitted | 2026-06-22 | human | TASK-033 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-017 behaviour, migration, and risks.
- 2026-06-17 — reconciled with task tree: D1–D7 proposed → approved (ratified by STORY-017 execution, already in-progress); `→ Tasks` wired to TASK-030…036; feature back-link added to those tasks. TASK-030 (host OAuth server) and TASK-031 (SQLite stores) already shipped.
- 2026-06-20 — TASK-034 plan grill: D6 → `changed` (greenfield, no migration/legacy fallback). Added D8 (from-scratch rebuild + switch live connections to JWT now), D9 (own by raw `sub`), D10 (scope the live Dragon list now). TASK-036 scope narrowed to deleting the dead legacy auth code.
- 2026-06-22 — TASK-033 plan grill (GitHub federation): added D11 (keep raw `github:{id}` as `sub`; mint DraCode JWT directly — internal-Guid model deferred), D12 (refresh store keys on the string `sub`), D13 (deny-all numeric-id allowlist gates admission, separate from authentication). Web callback hands the token to the SPA via URL fragment (button + hash bootstrap deferred to TASK-056); device flow needs no GitHub-specific code (transitively allowlist-gated).
