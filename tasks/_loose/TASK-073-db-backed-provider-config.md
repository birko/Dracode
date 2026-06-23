---
id: TASK-073
parent: null
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-06-23
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Store LLM provider configuration in the database (editable, not files/env)

## Context

Today LLM provider configuration is split across files and environment variables and is awkward to
change at runtime:

- `provider-config.json` — provider enable/endpoints (loaded by `ProviderConfigurationService`).
- `user-settings.json` — per-agent-type provider/model overrides (`UserSettings`,
  `koboldAgentTypeSettings`, `koboldProvider`/`koboldModel`, `dragonProvider`, etc.).
- `appsettings.json` / `appsettings.{Env}.json` — base provider config (providers disabled by default).
- **API keys via env vars** (`ANTHROPIC_API_KEY`, `ZHIPU_API_KEY`, `OPENAI_API_KEY`, …).

Editing a provider or key means touching files/env and usually restarting the server. There is no
runtime, queryable, editable source of truth. This blocks easy day-to-day operation (e.g. swapping a
key to run the in-review live verifications) and any future admin/management UI.

Move provider configuration into the database (following the existing Birko.Data.SQL pattern —
`SqlPlanRepository` / `SqlUsageRepository` / `CircuitBreakerEntity`) so it is transactional,
queryable, and editable at runtime without redeploying. **API keys are secrets** — they must be
encrypted at rest, not stored in plaintext (see EPIC-001 / TASK-001 "encrypted token storage" for the
encryption approach to reuse).

This is dev-facing infrastructure; it is **not** agent *working state* (so it is deliberately not
under EPIC-016, which scopes to plans/analysis/reasoning). It may graduate to its own epic
("runtime configuration & secrets management") if it grows.

## Acceptance criteria

- [ ] A provider-config DB entity + repository (mirroring `SqlPlanRepository`/`SqlUsageRepository`): provider name, enabled, endpoint/base-url, model defaults, and the per-agent-type override settings currently in `user-settings.json`
- [ ] API keys stored **encrypted at rest** (reuse the EPIC-001/TASK-001 encrypted-storage mechanism); never persisted or logged in plaintext
- [ ] `ProviderConfigurationService` reads provider config from the DB (DB is source of truth); env vars / `provider-config.json` become a fallback/seed, not the authority
- [ ] One-time idempotent import: on first run, existing `provider-config.json` + `user-settings.json` + env keys are imported into the DB, then the DB takes over
- [ ] Config is editable at runtime (changes take effect without a server restart — e.g. invalidate/reload the provider cache); decide the edit surface (REST `/api/v1` admin endpoint and/or Web UI) — likely a follow-up task
- [ ] Tests: round-trip persistence, encryption at rest (no plaintext key in the row), DB-over-file precedence, idempotent import, runtime reload picks up a changed provider/key

## Out of scope

- The admin **UI** for editing providers (separate task once the store + API exist)
- Migrating non-provider config (task/security/agent settings already in `projects.json`)
- Agent working-state artifacts (EPIC-016 owns those)
- A secrets *vault* integration (Azure Key Vault, etc.) — DB-encrypted at rest is the target here

## Human test plan

- [ ] Change a provider's API key / model in the DB (via the chosen edit surface) and confirm the next agent run uses the new value with no server restart
- [ ] Inspect the stored row → the API key is ciphertext, not plaintext

## Implementation plan

_Populated by `/tasks plan TASK-073` — leave empty until then._
