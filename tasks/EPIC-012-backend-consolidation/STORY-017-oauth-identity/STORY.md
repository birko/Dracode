---
id: STORY-017
parent: EPIC-012
status: planned
created: 2026-05-28
---

# OAuth/OIDC authentication and per-caller identity

## User story

As an operator wanting to expose KoboldLair to multiple clients (CLI on my laptop, Discord bot, CI runner, future teammates), I want real per-caller identity so I can audit who did what and revoke individual callers without breaking others. Local-only single-user installs should keep working with zero config.

## Behaviour

- GitHub OAuth as the first IdP (configurable to add more later — Microsoft, Google, generic OIDC)
- JWT validation middleware in front of `/api/v1/...` and `/dragon`, `/wyvern`, `/kobold`
- Stable `sub` claim → `User` record in DB (Birko.Data.SQL); projects gain `ownerId` foreign key
- Existing rows in `projects.json` migrated to a system `ownerId = "legacy"` user; future creates require a real owner
- Service-account JWT path for non-human callers (Discord bot, CI):
  - Operator generates a long-lived JWT via Web UI or `koboldlair keys create --service "discord-bot"`
  - JWT carries `{ sub: "service:discord-bot", scopes: ["projects:write", "runs:write"] }`
- **Local daemon mode bypasses auth entirely**: when `KoboldLair.Server` starts with `--daemon` and binds to `127.0.0.1`, it trusts the OS user and skips JWT validation. Remote daemons (any non-loopback bind) require auth.
- CLI login flow: `koboldlair login` opens browser, completes GitHub OAuth, caches token in `~/.koboldlair/auth.json`
- Web UI: existing login UI replaced by GitHub OAuth redirect

## Migration

- New SQL tables: `users`, `api_keys` (service accounts), `oauth_state` (in-flight OAuth flows)
- `projects.json` migration adds `ownerId` field; absent → `"legacy"` sentinel
- `WebSocketAuthenticationConfiguration` (the current shared-token system) marked deprecated; remains functional one release as a fallback for migration period, then removed

## Risks

- OAuth IdP unreachable → users can't auth locally. The local-daemon bypass mitigates the common case, but document the failure mode.
- JWT secret rotation requires server restart; document the operator playbook.
- Per-user project scoping: when an OAuth user looks at `GET /api/v1/projects`, do they see only their own, or all? Default to own; admins can opt into a `?scope=all` query param for ops scenarios.
