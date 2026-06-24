---
id: TASK-073
parent: null
feature: null
status: in-progress
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

## Decisions (agreed 2026-06-24)

- **Encryption:** AES-encrypt provider API keys at rest via `Birko.Security` (`AesEncryptionProvider`); the master key is resolved through `Birko.Security.ISecretProvider`. **Phase 1** default source = config/appsettings (dev: `dotnet user-secrets`). **Phase 2** = swap to Azure Key Vault / HashiCorp Vault (`Birko.Security.AzureKeyVault` / `.Vault`) — DI/config-only, no code change. **Not** coupled to TASK-001 (that's OS-keychain, machine-local, CLI-oriented).
- **Edit surface:** DB store + service + an **admin-gated `/api/v1/providers` REST CRUD** (Web UI is a follow-up).
- **Env keys:** imported once on first run, then **DB is the sole authority** (no runtime env override).
- **Models:** a provider exposes **many switchable models** — modelled as a normalized child table (`ProviderModelEntity`), not a single `DefaultModel` string. Each model row is individually enable/disable-able; one is the default; per-agent settings select one.

## Acceptance criteria

- [ ] DB entities + repository (mirror `SqlPlanRepository`/`PlanEntity`): `ProviderConfigEntity` (name, type, display, enabled, base-url, requires-key, compatible-agents, non-secret config, encrypted api-key) + `ProviderModelEntity` (per-provider switchable models: model id, display, enabled, is-default) + `AgentProviderSettingEntity` (replaces `user-settings.json`: agent/agent-type → provider + selected model)
- [ ] A provider can carry **multiple models**; the UI/API can list them, mark one default, enable/disable individuals; a per-agent setting picks one of the provider's models
- [ ] API keys stored **encrypted at rest** via `Birko.Security` AES + `ISecretProvider`-resolved master key; never persisted or logged in plaintext
- [ ] `ProviderConfigurationService` reads from the DB (source of truth) via an in-memory cache + `InitializeAsync` (startup, mirroring `ProviderCircuitBreaker` rehydration); its **public API is unchanged** so existing callers (factories, Drake) don't change; key decryption replaces the env-var read
- [ ] One-time idempotent import on first run: `appsettings.Providers` + `user-settings.json` + env API keys → DB (keys encrypted), then DB takes over; env ignored at runtime thereafter
- [ ] Admin-gated `/api/v1/providers` REST CRUD: list/create/update/enable-disable providers, manage their models, set an api-key (write-only — never returned), and read/set agent→provider/model assignments; gated by `ManageConfig`
- [ ] Runtime reload: editing a provider/key/model via the API takes effect on the next agent run without a server restart (cache invalidation on write)
- [ ] Tests: repo round-trip; encryption at rest (DB row holds ciphertext, not the key); DB-over-env precedence; idempotent import; model add/switch/default; REST CRUD incl. admin-gating (403 without `ManageConfig`) and api-key never echoed

## Out of scope

- The provider-admin **Web UI** (separate follow-up task once the store + REST API exist)
- **Phase 2 Vault** wiring (Azure KV / HashiCorp) — the `ISecretProvider` seam is built now; the actual Vault provider registration is a follow-up
- Migrating non-provider config (task/security/agent settings already in `projects.json`)
- Agent working-state artifacts (EPIC-016 owns those)
- Per-model pricing/context-window metadata (the `ProviderModelEntity` leaves room; cost-tracking config keeps its pricing table for now)

## Human test plan

- [ ] Change a provider's API key / model in the DB (via the chosen edit surface) and confirm the next agent run uses the new value with no server restart
- [ ] Inspect the stored row → the API key is ciphertext, not plaintext

## Implementation plan

### Grounding (verified against current code)
- `ProviderConfigurationService` reads providers from `IOptions<KoboldLairConfiguration>.Providers` (from `appsettings.json`) and agent assignments from `user-settings.json` (sync file IO in the ctor via `.GetAwaiter().GetResult()`). API keys are read from **env vars** at resolve time (`GetApiKeyEnvironmentVariable` → `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, …) or from `ProviderConfig.Configuration["apiKey"]`.
- `ProviderConfig` (model) has a single `DefaultModel` string and **no model list** — model switching is just a free string in `UserSettings.*Model` / `KoboldAgentTypeProviderSettings.Model`.
- Public API consumed by callers (don't break): `GetProviderForAgent`, `GetProviderForKoboldAgentType`, `GetProviderSettingsForAgent`, `GetProviderSettingsForKoboldAgentType`, `GetAllProviders`/`GetAvailableProviders`, `SetProviderForAgent`, `SetProviderForKoboldAgentType`, `GetUserSettings`, `ValidateProvider`, `GetDefaultProvider`. Callers: `DrakeFactory`, `WyrmFactory`, `WyvernFactory`, `Drake`, `ProjectService`, `KoboldFactory` (via DI).
- DB pattern to mirror: `DraCode.KoboldLair/Data/Entities/PlanEntity.cs` + `Data/Repositories/Sql/SqlPlanRepository.cs`; interfaces exist (`IProjectRepository`, `ITaskRepository`). Startup rehydration pattern: `ProviderCircuitBreaker.InitializePersistenceAsync()` called from `Program.cs`.
- Encryption primitives available: `Birko.Security/Encryption/AesEncryptionProvider.cs`, `Birko.Security/Core/ISecretProvider.cs` (+ `Birko.Security.AzureKeyVault`, `Birko.Security.Vault`).

### Ordered steps
1. ✅ **Entities** (`Data/Entities/`): `ProviderConfigEntity` (Name, Type, DisplayName, Enabled, BaseUrl, RequiresApiKey, DefaultModel, CompatibleAgents json, Configuration json [no secret], `ApiKeyCiphertext`, timestamps); `ProviderModelEntity` (ProviderName, ModelId, DisplayName, Enabled, SortOrder); `AgentProviderSettingEntity` (AgentKey e.g. `dragon`/`kobold:csharp`, Provider, Model). _(default model = `ProviderConfigEntity.DefaultModel` naming a model row — dropped the redundant `IsDefault` column.)_
2. ✅ **Repository**: `SqlProviderConfigRepository` (mirrors `SqlPlanRepository`; concrete, no interface — matches that analog) — get-all/get/upsert/delete provider, `IsEmptyAsync` (import guard), `SetApiKeyCiphertextAsync` (metadata upsert preserves the key), models CRUD, agent-setting upsert/delete. Immediate writes. _(8 repo tests green.)_
3. ✅ **Secret/cipher seam**: `ConfigSecretProvider` (Phase-1 in-memory `ISecretProvider` seeded from config) + `ProviderKeyCipher` over Birko `AesEncryptionProvider` (AES-256-GCM; master key resolved via `ISecretProvider`, stretched with SHA-256; `InitializeAsync` once, then sync Encrypt/Decrypt). Never logs plaintext. _(5 cipher tests.)_
4. ✅ **Service refactor**: `ProviderConfigurationService` is now **dual-mode** — legacy (appsettings + `user-settings.json` + env, unchanged) when no repo; **DB-backed** when a repo + cipher are supplied. `InitializeAsync()` resolves the master key, one-time-imports the legacy config into an empty DB (keys encrypted), then loads an in-memory cache; the env-var key read is replaced by `ProviderKeyCipher.Decrypt`; `Set*` edits persist to the DB (+ kobold:* reconciliation) and the cache. Every public signature unchanged → callers untouched. _DI still binds legacy mode (zero runtime change) until step 6 flips it._ _(5 DB-service tests: import/encrypt-at-rest, DB-over-env authority, cross-service persistence, reconciliation, load.)_
5. **Admin REST** (`Api/ProvidersEndpoints.cs`, `MapProviderEndpoints` on the `/api/v1` group, `.RequirePermission(ManageConfig)`): GET providers (+models, **key never returned** — only a `hasKey` bool), POST/PATCH provider, PATCH `/providers/{name}/key` (write-only), POST/DELETE `/providers/{name}/models`, GET/PUT agent assignments.
6. **DI + startup** (`Program.cs`): register repo, `ISecretProvider`, `ProviderKeyCipher`; `await providerConfig.InitializeAsync()` at startup (next to circuit-breaker rehydrate); `apiV1.MapProviderEndpoints()`.
7. **Tests**: `SqlProviderConfigRepositoryTests` (round-trip, ciphertext-at-rest, models); `ProviderConfigurationServiceTests` (import idempotency, DB-over-env, model switch, runtime reload); `ProvidersEndpointsTests` (CRUD, `ManageConfig` gating → 403, key never echoed).

### Tradeoffs / risks
- **Blast radius**: keeping the service's public API identical confines the change to the service internals + DI; factories/Drake untouched. The risk is the import path — must be idempotent and not clobber a DB already edited (guard on "table empty", not "file exists").
- **Master key in config (Phase 1) ≈ env exposure on a single box** — buys DB-leak protection, not host-compromise; Phase 2 Vault closes that. The `ISecretProvider` seam keeps Phase 2 code-free.
- **Model free-text vs enumerated**: allow selecting a model id not yet in `ProviderModelEntity` (forward-compat for brand-new models) but surface a warning, so a new model works before someone curates the list.
- **Sync ctor → async init**: the service can no longer do file IO in its ctor; move load to `InitializeAsync` (startup-awaited) to avoid sync-over-async.

### Critical files
- **New** `Data/Entities/ProviderConfigEntity.cs`, `ProviderModelEntity.cs`, `AgentProviderSettingEntity.cs`
- **New** `Data/Repositories/{IProviderConfigRepository.cs, Sql/SqlProviderConfigRepository.cs}`
- **New** `Services/ProviderKeyCipher.cs` (over `Birko.Security` AES + `ISecretProvider`)
- **New** `DraCode.KoboldLair.Server/Api/ProvidersEndpoints.cs`
- `Services/ProviderConfigurationService.cs` — repo-backed cache + `InitializeAsync` + import; public API unchanged
- `DraCode.KoboldLair.Server/Program.cs` — DI (repo, secret provider, cipher), startup init, `MapProviderEndpoints`
- **New** tests: `SqlProviderConfigRepositoryTests`, `ProviderConfigurationServiceTests`, `ProvidersEndpointsTests`
