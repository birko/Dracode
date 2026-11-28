---
id: TASK-230
parent: EPIC-016
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-28
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-228, Birko.Framework/TASK-1013]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: 95
github-issue: null
jira-key: null
---

# The OAuth stores and client names on the store factory

## Context

Split from TASK-228 on 2026-11-28 (the owner's call). Birko ships OAuth server stores only for SQLite and in memory,
and their one-time claims (redeeming a code, rotating a refresh token) are SQLite conditional updates, so they cannot be
built from a store factory. Birko TASK-1013 (filed by `birko-framework-44`, P2) adds the five stores over any
`IStoreFactory`, with atomic claims per backend and a client lookup by name. Until then KoboldLair keeps its OAuth
stores on the SQLite file (`AddOAuthServerStores(useSqlite: true, …)`) whatever `KoboldLair:Data:Provider` says, and
`OAuthClientNames` reads `oauth_clients` from that file.

## Acceptance criteria

- [x] The OAuth stores come from the configured database's store factory (Birko TASK-1013's registration)
- [x] ~~`OAuthClientNames` asks the client store for a name~~ `OAuthClientNames` reads the clients through the store
      factory instead of the SQLite table: the store's `GetByNameAsync` is exact, and a key's name is checked in any case
- [x] The key tests (create, token, revoke, name clash) pass on SQLite and InMemory

## Human test plan

- [x] On a throwaway server: create an API key, get a token with it, change a specification, revoke it — as before

## Implementation plan

1. Red: `EveryStoreOnEveryDatabaseTests` writes an OAuth client through one factory, reads it back through another and
   checks the name in another case through `OAuthClientNames(factory)`, which did not exist (it took the SQLite file).
2. `Auth/OAuthStores.cs` replaces `SqliteOAuthStores.cs`: `AddKoboldLairOAuthStores(IStoreFactory)` = Birko's
   `AddOAuthServerStores(factory)`, the `ClaimsJson` column repair on a SQL backend, and `OAuthClientNames`.
3. `OAuthClientNames(IStoreFactory)` reads every `OAuthClientEntity` through `StoreRepository`, case-insensitive.
4. Program.cs registers the stores on `RepositoryFactory.StoreFactoryFor` of the configured database; the tests (keys,
   federated sessions, the OAuth flows) use a SQLite or InMemory factory; DraCode.Birko no longer imports
   `Birko.Security.OAuth.Server.SqLite` or `.InMemory`.
5. CLAUDE.md (Which database, its configuration block, the WAL section).

## Notes

- 2026-11-28: Birko TASK-1013 started, shape decided by the owner (from `birko-framework-44`): one-winner claims become a
  store capability (`IAsyncConditionalUpdateStore<T>.UpdateIfAsync`, on SQL, MongoDB, RavenDB, Cosmos DB, Elasticsearch
  and InMemory; JSON/XML refused); `OAuthClient` and `RefreshTokenRecord` persist as one entity each on every backend, so
  the client store answers predicates and gains `GetByNameAsync` (`OAuthClientNames` can ask it);
  `services.AddOAuthServerStores(IStoreFactory)` registers all five. `Birko.Security.OAuth.Server.SqLite` will be deleted,
  but only after DraCode switches: Birko says when the factory stores land; switch then and tell `birko-framework-44`.
- 2026-11-28: Birko TASK-1013 landed in the Framework working tree (commits 7a13a8d9, cd98017e; local, not pushed when
  told). `services.AddOAuthServerStores(IStoreFactory)` in the core package (namespace
  `Birko.Security.OAuth.Server.Extensions`; no new import in DraCode.Birko), refusing JSON/XML at registration; the
  stores by hand are `Birko.Security.OAuth.Server.Stores.Factory.Factory*Store`. `IOAuthClientStore.GetByNameAsync` on
  every implementation, so `oauth_clients` need not be read directly. Same tables and columns as the SQLite package (an
  existing database reads and writes as is). One-winner claims live on every backend. Known gap Birko TASK-1023: tables
  the factory stores create fresh have no index on ClientId / token hash yet. When DraCode no longer references
  `Birko.Security.OAuth.Server.SqLite` (code, tests, the DraCode.Birko.csproj import), tell `birko-framework-44`; then
  the package is deleted.
- 2026-11-28: done. Full suite 774 passed; the auth and every-database tests 90/90 with `BIRKO_REQUIRE_LIVE=1` on
  PostgreSQL and MongoDB. Nothing in DraCode references `Birko.Security.OAuth.Server.SqLite` or `.InMemory`.
- Human test, two throwaway servers (Testing, scratch folders, own random JWT secrets):
  - SQLite, on a copy of the database TASK-228's test wrote with Birko's old SQLite stores, its `service_keys` row
    removed so only `oauth_clients` held the name `tt228`: a key named `TT228` was refused (409), so the old table was
    read and the name matched in another case. A new key: created, a token, a project and a specification change made
    with it (version 2), revoked, and the next token refused (400).
  - PostgreSQL, a fresh database: the same key life, all passed; `oauth_clients`, `oauth_refresh_tokens` and
    `service_keys` were in PostgreSQL (the client disabled after the revoke) and no SQLite file was made. Database dropped.
