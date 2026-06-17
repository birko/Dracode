---
id: TASK-031
parent: STORY-017
feature: null
status: done
priority: P1
assignee: ai
created: 2026-06-11
status-changed: 2026-06-17
depends-on: [TASK-030]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# SQLite-backed OAuth server stores

## Context

The OAuth-server store interfaces are each `IAsyncStore<T>` plus default-method named lookups, and the models are `AbstractModel` descendants — so production persistence is **five trivial subclass declarations** over `AsyncSQLiteStore<T>` (the verified `AsyncSQLiteStore<T>` → `AsyncDataBaseBulkStore` → `IAsyncBulkStore<T>` → `IAsyncStore<T>` chain). SQLite matches the rest of DraCode persistence (`SqlPlanRepository`); no Postgres project exists. No bespoke subproject, no `*Entity`/`EntityMapper` layer.

## Acceptance criteria (revised 2026-06-16 — see "Design resolved" below)

- [x] Four scalar models persist via thin `: AsyncSQLiteStore<T>, I…Store` subclasses with **no entity layer**: `SqlAuthorizationCodeStore`, `SqlRefreshTokenStore`, `SqlDeviceCodeStore`, `SqlConsentStore`. Their tables are registered via `DataBase.RegisterTableName(...)` (the POCOs carry no `[Table]` attribute) — the SQL field-mapper auto-maps their scalar properties.
- [x] `OAuthClient` (the only model with `List<string>` columns) persists via `OAuthClientEntity` (scalar columns + a JSON column for RedirectUris/AllowedGrantTypes/AllowedScopes) wrapped by `SqlOAuthClientStore : IOAuthClientStore` (model↔entity mapping; `GetByClientIdAsync` queries the indexed `ClientId` column).
- [x] `AddOAuthServerStores(useSqlite, dbPath)` DI extension satisfies TASK-030's five injected store interfaces — SQLite when `DataStorageConfig.DefaultBackend == SqLite`, else the existing in-memory stores.
- [x] Tables created on startup (`SetSettings(new PasswordSettings(dbDir, dbFile))` + `CreateSchemaAsync()`, same DB-file as `SqlPlanRepository`).
- [x] Integration test: TASK-030's grant flows pass against the SQLite stores over a temp db file; records survive a fresh store instance pointed at the same file (incl. `OAuthClient` collections round-tripping through the JSON column).
- [x] Named lookups (`GetByClientIdAsync`, `GetByCodeAsync`, `GetByDeviceCodeAsync`, `GetByUserCodeAsync`, `GetByHashAsync`, `GetAsync`) resolve correctly.

## Implementation (2026-06-17)

- `DraCode.KoboldLair.Server/Auth/SqliteOAuthStores.cs` — four scalar stores, `OAuthClientEntity` + `SqlOAuthClientStore`, and the `AddOAuthServerStores(useSqlite, dbPath)` DI extension (registers SQLite-backed or in-memory).
- `DraCode.KoboldLair.Server/Program.cs` — replaced the five hard-coded in-memory registrations with `AddOAuthServerStores(...)`, reading `KoboldLair:Data` backend + `KoboldLair:ProjectsPath` to resolve the db path via `RepositoryFactory.ResolveSqLitePath`.
- `DraCode.KoboldLair.Tests/Auth/SqliteOAuthStoreTests.cs` — 3 integration tests over a temp db (client_credentials, device_code pending→approved, collection round-trip across a fresh store). Added a `ProjectReference` from Tests → Server.
- Verified: full `DraCode.slnx` build clean; all 11 Auth tests pass (8 existing in-memory + 3 new SQLite).
- **Note:** `SqlOAuthClientStore.ReadAsync(Expression<Func<OAuthClient,bool>>)` throws `NotSupportedException` — arbitrary client predicates can't be translated to the entity table, and no handler uses it (verified: client store is only touched via `GetByClientIdAsync` + `Create/Update/Delete`).

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

## ✅ Design resolved (2026-06-16) — blocker was half-right

Re-verified against the framework source (`Birko.Data.SQL/SQL/DataBase_Table.cs`, `SQL/Fields/AbstractField.cs`, `SQL/DataBase_Field.cs`). The blocker's premise was **partly wrong**:

- **Fields auto-map without attributes.** `CreateAbstractField` maps every public scalar property by CLR type (string/DateTime/bool/Guid/int/enum) — no `[…Field]` attribute required. `List<string>` returns `null` and is silently skipped (not an NRE).
- **The NRE came only from the missing `[Table]` attribute.** `ComputeTable` returns null when neither a `[Table]` attribute nor a fluent override exists → empty mapping → NRE on insert. `DataBase.RegisterTableName(type, name)` is the documented fluent hook that supplies the table name **without** owning the type — no entity needed.
- **The type constraint is fine.** `AsyncSQLiteStore<T> where T : Birko.Data.Models.AbstractModel` is the *same* base the OAuth POCOs already derive from, so `AsyncSQLiteStore<AuthorizationCode>` etc. compile directly.

So an entity+mapper is needed for **`OAuthClient` only** (its three `List<string>` columns would otherwise vanish). The other four models persist natively via `RegisterTableName` + a one-line subclass. Handlers only use `GetByClientIdAsync` + `Create/Update/Delete` on the client store and named lookups (→ `ReadAsync(filter)`) on the rest — all covered.

**Prerequisite unblocked along the way:** the framework source-duplication (CS0436, triple-compiled `AbstractModel`) that made even the trivial subclass fail to *compile* is now fixed — `DraCode.Birko` is the single source-compiler of the framework; KoboldLair/Server/Tests consume it compiled. That work stands independent of this task.
