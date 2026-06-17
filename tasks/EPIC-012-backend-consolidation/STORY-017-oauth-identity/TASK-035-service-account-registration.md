---
id: TASK-035
parent: STORY-017
feature: FEATURE-019
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-030, TASK-032]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Service-account client registration

## Context

Non-human callers (Discord bot, CI) authenticate via `client_credentials` confidential clients with scoped tokens (`{ sub: "service:discord-bot", scopes: ["projects:write","runs:write"] }`). Operators register these clients via the Web UI and/or `koboldlair keys create --service "discord-bot"`.

## Acceptance criteria

- [ ] Operator can register a confidential OAuth client (Web UI form + CLI `keys create`)
- [ ] Issued client_credentials token carries `sub: "service:<name>"` + requested `scopes`
- [ ] Scopes enforced at endpoints (via TASK-032's `PermissionEndpointFilter`)
- [ ] Client secret shown once on creation, stored hashed (server's SHA-256 hashing)
- [ ] Revocation: operator can disable a client without affecting others
- [ ] Tests: scoped token allows in-scope calls, rejects out-of-scope (403)

## Out of scope

- The Discord bot itself (EPIC-013 / STORY-020)
- Human OAuth (TASK-033)

## Human test plan

- [ ] Create a service client via CLI, use its token to `POST /api/v1/runs` (in-scope, succeeds) and `DELETE /api/v1/projects/{id}` (out-of-scope, 403); disable the client → token rejected

## Implementation plan

_Populated by `/tasks plan TASK-035` — leave empty until then._
