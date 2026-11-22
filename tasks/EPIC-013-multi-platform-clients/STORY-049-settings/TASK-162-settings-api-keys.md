---
id: TASK-162
parent: STORY-049
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-161]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Settings: service-account API keys

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Service accounts are OAuth clients: created with `POST /register`, disabled with `POST /register/{clientId}/disable`,
used through the client-credentials token; the CLI's `koboldlair keys` does create and revoke. Nothing lists them —
the OAuth client store cannot query (`ReadAsync(predicate)` throws). This task adds an owner-scoped
`GET /api/v1/keys` (the caller's service accounts: id, name, created, disabled), recording the creator as owner if
registration does not already, and the Settings → API keys tab: create (the secret shown once, with copy), list,
revoke. This settles the old "list has no server support" question by building the list.

Decisions: FEATURE-026 D6.

## Acceptance criteria

- [x] `GET /api/v1/keys` lists only the caller's service accounts
- [x] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [x] Creating a key shows its secret once with a copy button; it is never shown again
- [x] Revoking a key disables it; a token request with it then fails
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Key scopes or expiry (not decided)

## Human test plan

- [x] Create a key, use it from the CLI, revoke it in the browser: the CLI's next call is refused
  - **Passed 2026-11-22:** see Notes (plain HTTP calls in the CLI's place: no CLI with keys exists yet, TASK-050–054)

## Implementation plan

Decided with the owner (2026-11-22): **any signed-in user** creates keys (no longer only `manage_users` through
`/register`); a key gets a **fixed set** — view_own, manage_projects, execute_agents — narrowed to its creator's
permissions. A key still acts as `service:<name>` (FEATURE-019 D14), so key names are unique.

1. Server: `service_keys` owner table (`ServiceKeyEntity`, `ServiceKeyRegistry`: SQLite on that backend, else memory);
   `/api/v1/keys` — `GET` (own; `scope=all` for an admin), `POST` (create, secret once), `POST {id}/revoke` (owner or
   admin, 404 otherwise) — mapped when OAuth is on; `/register` records its admin as owner; capabilities gain
   `keysEnabled` and a hint.
2. Client: Settings → API keys (`kobold-api-keys-page`, Shell `BaseListPage`) for every signed-in user: list, create
   (secret dialog with Copy), Revoke with a confirm.
3. Tests: .NET (owners, `github:` subject, admin, narrowing, names, `/register`, OAuth off); e2e keys spec.

## Notes

- **Server** (`KeysEndpoints`, `ServiceKeyRegistry`, `ServiceKeyEntity`): `GET /api/v1/keys` lists the caller's keys
  (`scope=all` gives an admin every key), each `{ clientId, name, ownerId, scopes, createdAt, disabled }`, the
  enabled flag read from the OAuth client store. `POST /api/v1/keys { name }` registers a confidential
  client_credentials client with `KeyPermissions` narrowed to the caller's (`*` keeps all), records the caller as
  owner, and answers 201 with `clientSecret` — the only time it leaves the server. A name is 1–100 characters without
  spaces and unique across all keys (409 otherwise: the key acts as `service:<name>`). `POST /api/v1/keys/{id}/revoke`
  disables the client (owner or admin; another owner's key reads as 404). Mapped only when
  `Authentication:OAuth:Enabled` (the token exchange needs the OAuth server); `/settings/capabilities` adds
  `keysEnabled` and `keysHint`. `/register` (admin) records its caller as the key's owner. The registry resolves its
  database when first used (the server's SQLite file on that backend), so a test host's settings apply — read at
  build time, the path was the shared test database and keys leaked between tests.
- **Client:** `kobold-settings-view` now shows an "API keys" tab to every signed-in user (`#/settings/keys`); the
  Providers and Agents tabs stay admin-only, and the "Nothing to set here" message is gone. `kobold-api-keys-page`
  (`BaseListPage`, no edit or delete): name, client id, what it can do, created, Active/Revoked; "New API key" asks for
  a name, then a dialog shows the client id and secret once with Copy buttons (cleared when it closes); "Revoke" asks
  through a confirm dialog.
- **Tests:** `KeysEndpointsTests` (6): a user creates a key, sees only their own (a `github:` subject sees only theirs,
  and gets 404 revoking another's), the secret is not in the list, the key gets a token until revoked and then not;
  an admin sees all with `scope=all` and may revoke any; a viewer's key holds only `view_own`; names unique and well
  formed; a `/register` key listed for its admin; with OAuth off the routes are not mapped and the capabilities say
  why. e2e `keys.spec.ts` (5: list, create with the secret once and Copy, revoke, cancel, off) and the settings
  spec's non-admin test updated — all 6 red on the old client. Suites .NET 578/578, e2e 154/154 (twice).
- **Human test** (throwaway Testing server with OAuth on, scratch copy of `projects160` with its paths moved, a
  non-admin user): Settings showed only "API keys"; "New API key" `human-cli` → the dialog showed its client id and a
  43-character secret, Copy put it on the clipboard, and after closing the page no longer held it. As the CLI would:
  `POST /token` (client_credentials) → a token with `view_own,manage_projects,execute_agents`, `GET /api/v1/projects`
  → 200 `[]` (the key owns no projects yet). Revoke in the browser (confirm) → the row reads Revoked, and the next
  `POST /token` → 400 `invalid_client: Unknown or disabled client`. No console errors.
- **Known gap:** clients registered through `/register` before `service_keys` existed are not in it, so a new key may
  take one of their names, and with it their `service:<name>` identity → TASK-194.
