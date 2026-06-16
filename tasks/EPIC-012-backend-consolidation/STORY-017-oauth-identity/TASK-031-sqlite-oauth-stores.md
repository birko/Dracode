---
id: TASK-031
parent: STORY-017
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-06-11
status-changed: 2026-06-16
depends-on: [TASK-030]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# SQLite-backed OAuth server stores

## Context

The OAuth-server store interfaces are each `IAsyncStore<T>` plus default-method named lookups, and the models are `AbstractModel` descendants — so production persistence is **five trivial subclass declarations** over `AsyncSQLiteStore<T>` (the verified `AsyncSQLiteStore<T>` → `AsyncDataBaseBulkStore` → `IAsyncBulkStore<T>` → `IAsyncStore<T>` chain). SQLite matches the rest of DraCode persistence (`SqlPlanRepository`); no Postgres project exists. No bespoke subproject, no `*Entity`/`EntityMapper` layer.

## Acceptance criteria

- [ ] Five subclasses declared: `SqlOAuthClientStore`, `SqlAuthorizationCodeStore`, `SqlRefreshTokenStore`, `SqlDeviceCodeStore`, `SqlConsentStore` (each `: AsyncSQLiteStore<T>, I…Store`)
- [ ] Registered in DI to satisfy TASK-030's injected store interfaces
- [ ] `SetSettings(new PasswordSettings(dbDir, dbFile))` + `InitAsync()` create tables on startup (same DB-file pattern as `SqlPlanRepository`)
- [ ] Integration test: TASK-030's grant flows pass against the SQLite stores (not just in-memory)
- [ ] Named lookups (`GetByClientIdAsync`, `GetByCodeAsync`, etc.) resolve correctly through the default `ReadAsync(filter)`

## Out of scope

- DraCode `User` table (TASK-034)
- Any change to the OAuth server handlers themselves

## Human test plan

- [ ] N/A — fully covered by automated tests

## Implementation plan

Grounded against the code (TASK-030 wiring at `DraCode.KoboldLair.Server/Program.cs:82-128`; in-memory stores at `Auth/InMemoryOAuthStores.cs`; reference pattern `SqlPlanRepository`).

**Key facts**
- Store interfaces (`Birko.Security.OAuth.Server.Stores`) each extend `IAsyncStore<T>` with default-method lookups that fall through to `ReadAsync(filter)` — no method overrides needed in subclasses.
- Models (`Birko.Security.OAuth.Server.Models`) are `AbstractModel` descendants → persist directly via `AsyncSQLiteStore<T>` (`Birko.Data.SQL.SqLite.Stores`).
- SQLite table creation is **`CreateSchemaAsync()`** (not `InitAsync()`), per `AsyncSQLiteStore<T>`/`SqlPlanRepository`. `PasswordSettings(location, name)` is `Birko.Configuration`.
- DB path: reuse the shared `RepositoryFactory.ResolveSqLitePath(dataConfig)` (`koboldlair.db`) — each store creates its own table by type, so one db file is fine.
- Backend selection mirrors the other repos: SQLite only when `DataStorageConfig.DefaultBackend == StorageBackend.SqLite`; otherwise keep the existing in-memory stores.

**Steps**
1. New file `Auth/SqliteOAuthStores.cs`: five subclasses `: AsyncSQLiteStore<T>, I…Store` (`SqlOAuthClientStore`, `SqlAuthorizationCodeStore`, `SqlRefreshTokenStore`, `SqlDeviceCodeStore`, `SqlConsentStore`), plus an `AddOAuthServerStores(this IServiceCollection)` extension that registers each interface as a singleton — SQLite-backed (SetSettings + CreateSchemaAsync) when configured, else the in-memory store.
2. `Program.cs`: replace the five hard-coded in-memory registrations (lines 86-90) with `builder.Services.AddOAuthServerStores();`. OAuthServer factory unchanged (still resolves the interfaces).
3. Integration test (`DraCode.KoboldLair.Tests/Auth/`): run TASK-030's grant flows (client_credentials + device_code at minimum, exercising a named lookup) against the SQLite stores over a temp db file; assert success + that records survive a fresh store instance pointed at the same file.
4. `dotnet build` + run the OAuth test suite.

**Risk to watch:** `OAuthClient` has `List<string>` properties (RedirectUris/GrantTypes/Scopes). If the SQLite store can't round-trip collection columns, the integration test catches it — fall back to a JSON-serialized column shim only if it actually fails.

## ⚠️ Blocker found (2026-06-16) — design needs revision

The "five trivial `: AsyncSQLiteStore<T>` subclasses" premise is **not viable**. The Birko SQL layer maps columns from `[Table]`/`[…Field]` attributes and stores collections as JSON string columns (see `PlanEntity`). The OAuth models (`OAuthClient`, …) are **upstream POCOs in `Birko.Security.OAuth.Server` that we don't own** — no Birko SQL attributes, and `List<string>` properties. A direct `AsyncSQLiteStore<OAuthClient>` builds an empty table mapping and throws `NullReferenceException` on insert (verified: `AbstractAsyncConnector.InsertAsync` → `table.Fields`). So SQLite persistence requires an **Entity + mapper layer after all** (the thing this task said to avoid):

- Define 5 `*Entity : AbstractDatabaseLogModel` types with `[Table]`/`[…Field]` attributes; a Guid PK; indexed lookup columns (ClientId / Code / TokenHash / DeviceCode + UserCode / UserId+ClientId); collections + the rest as a JSON payload column.
- Each `Sql…Store : AsyncSQLiteStore<…Entity>, I…Store` overrides the CRUD + the named lookups (`GetByClientIdAsync`, …) to map model ↔ entity (the inherited `ReadAsync(Expression<Func<Model,bool>>)` can't run against an entity store, so override the named lookups to query the entity columns directly).

Reset to `todo` pending acceptance-criteria revision (the criteria below assume the no-mapper design).

**Prerequisite unblocked along the way:** the framework source-duplication (CS0436, triple-compiled `AbstractModel`) that made even the trivial subclass fail to *compile* is now fixed — `DraCode.Birko` is the single source-compiler of the framework; KoboldLair/Server/Tests consume it compiled. That work stands independent of this task.
