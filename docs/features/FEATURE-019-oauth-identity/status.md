---
id: FEATURE-019
generated: 2026-06-17
---

# OAuth/OIDC authentication and per-caller identity — Status

> Auto-generated rollup. PM/stocktaker audience, no code jargon.

**Phase:** building

## Decisions at a glance

| State | Count |
|-------|-------|
| ✅ approved | 7 |
| ✏️ changed | 0 |
| ⏸️ deferred | 0 |
| ❌ removed | 0 |
| 💭 proposed (undecided) | 0 |

## Build progress

2 / 7 tasks done.

| Task | Status |
|------|--------|
| Host the sign-in server inside KoboldLair | ✅ done |
| Store sign-in records durably | ✅ done |
| Enforce sign-in in front of the live surfaces (with local-machine bypass) | ⬜ next up |
| Sign in with GitHub | ⬜ todo |
| Give every user a record and every project an owner | ⬜ todo |
| Issue scoped credentials for bots and CI runners | ⬜ todo |
| Retire the old shared-token login | ⬜ todo |

## What can be tested now

The sign-in server is hosted and its records persist across restarts, but nothing is
enforced yet — every surface is still open until the validation step lands. No
user-facing sign-in to exercise yet.

## Prototype
Pending — backlog item; prototype decision deferred (the work is being built directly from the approved decisions).

## Next step
Build the validation step (enforce sign-in in front of the HTTP and live-connection
surfaces, with the local-machine bypass). This is the unblocker for the user-record,
GitHub-login, and old-login-retirement work that follows.
