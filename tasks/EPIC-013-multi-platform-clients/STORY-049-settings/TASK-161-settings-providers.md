---
id: TASK-161
parent: STORY-049
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-145]
blocks: [TASK-162, TASK-163]
findings: []
pr: 43
github-issue: null
jira-key: null
---

# Settings: providers

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

The current settings view (`b-tabs`, General and Providers) uses `/wyvern` provider commands. Rebuild it at
`#/settings` on `/api/v1/providers` (admin CRUD, mapped only when a provider master key is set) with the Shell CRUD
and form-modal pages. A non-admin does not see the Providers tab; when the endpoints are not mapped, the tab says how
to enable them. What the General tab holds today (client settings) moves into the new screen. The old view goes.

Decisions: FEATURE-026 D20.

## Acceptance criteria

- [x] An admin lists, adds, edits, validates and removes providers through `/api/v1/providers`
- [x] A non-admin sees no Providers tab; with the endpoints unmapped the tab explains why
- [x] The old settings view and the legacy `styles.css` it relied on are removed
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- API keys (TASK-162)
- Budgets (TASK-163)
- Settings on phones (D21)

## Human test plan

- [x] Add a provider with a wrong key: validation fails with a readable message; fix it: it validates and saves
  - **Passed 2026-11-22:** see Notes

## Implementation plan

Decided with the owner (2026-11-22): Validate is a **real connection test**; the old **General tab is dropped** (it
saved nothing; the theme lands with TASK-188); **providers only** — models, the default provider and agent
assignments go to TASK-192.

1. Server: `PUT /api/v1/providers/{name}` (the Shell's edit), `POST /providers/{name}/validate` (the configuration
   check, then one short request through the provider), and `GET /api/v1/settings/capabilities` (always mapped:
   `canManageProviders`, `providersEnabled`, a hint when off).
2. Client: `kobold-settings-view` at `#/settings` (tabs from the capabilities) and `kobold-providers-page`, Birko
   Shell's `BaseListPage` over `/api/v1/providers`; the key is write-only (`PATCH …/key` after the save).
3. Remove the old view, its General route and module option, and `wwwroot/styles.css`.
4. Tests: .NET endpoint tests (a fake OpenAI-compatible model on localhost for validation); e2e settings spec.

## Notes

- **Server** (`ProvidersEndpoints`, new `SettingsEndpoints`): `PUT /providers/{name}` upserts by the route's name
  (what the Shell's CRUD page sends for an edit; the key is kept). `POST /providers/{name}/validate` runs
  `ValidateProvider` (exists, enabled, key set) and then one short request through the provider
  (`KoboldLairAgentFactory.CreateLlmProvider`, 30 s timeout); it answers `{ name, valid, message }`, the message being
  the provider's own error on a refusal. `GET /settings/capabilities` is always mapped: `canManageProviders`
  (`manage_config` or `*`), `providersEnabled` (a provider master key is set) and a hint naming
  `KoboldLair:ProviderConfig:MasterKey` when off. **Found and fixed:** the upsert stored `baseUrl` only in the entity's
  column, which the runtime never reads (it reads `ConfigurationJson`), so a provider created through the API ignored
  its base URL; the upsert now writes it into the configuration as well.
- **Client:** `kobold-settings-view` (`#/settings`, `#/settings/providers`) shows a Providers tab to admins, the
  hint when provider management is off, and "Nothing to set here for your account yet" otherwise.
  `kobold-providers-page` (`BaseListPage`): columns name, display name, type, default model, enabled, API key
  (Set / Missing / Not needed); the form has name (fixed on edit), display name, type (the 15 factory types), base URL,
  default model, agents it serves, enabled, needs an API key, and a write-only API key sent with `PATCH …/key` after
  the save; a "Validate" row action shows the server's answer as a toast. A new provider starts enabled, needing a key,
  for all agents (`onFormReady`: the page clears the form on create, so the schema's defaults were lost — caught by the
  e2e). `build.js` gains the `birko-web-shell/pages` and `birko-web-components/form-utils` aliases. Removed: the old
  view (on `/wyvern`), the `#/settings/general` route and module option, and the unused `wwwroot/styles.css`.
- **Tests:** `ProvidersEndpointsTests` +4: validation against a fake OpenAI-compatible model on localhost (no key →
  the configuration answers and the model is not called; wrong key → refused; right key → valid; 404 unknown; 403 for
  a viewer); edit by `PUT` keeps the key and stores the base URL; capabilities for an admin and a viewer; off without a
  master key. e2e `settings.spec.ts` rewritten (7 tests: list, add with key, edit with `PUT`, validate both ways,
  delete, non-admin, off) and `routes.spec.ts` updated; all 8 changed tests red on the old client. Suites .NET 571/571,
  e2e 142/142 (twice).
- **Human test** (throwaway Testing server, scratch copy of `projects160` with its paths moved, a throwaway master
  key so providers live in the database, an admin token, the LAN model): "New Provider" `lan-test2` (vllm, the LAN
  base URL) with a wrong key → Validate: "The provider answered with an error: vLLM API Error (Unauthorized):
  Authentication Error, Invalid proxy server token passed…"; Edit → the right key → Validate: "Connected: the provider
  answered (model finstat)". The key never appeared on the page; no console errors.
- **Follow-up:** models, the default provider and agent assignments → TASK-192.
