---
id: TASK-230
parent: EPIC-016
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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
pr: null
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

- [ ] The OAuth stores come from the configured database's store factory (Birko TASK-1013's registration)
- [ ] `OAuthClientNames` asks the client store for a name instead of reading the SQLite table
- [ ] The key tests (create, token, revoke, name clash) pass on SQLite and InMemory

## Human test plan

- [ ] On a throwaway server: create an API key, get a token with it, change a specification, revoke it — as before

## Implementation plan

_Populated by `/tasks plan TASK-230` — leave empty until then._

## Notes

- 2026-11-28: Birko TASK-1013 started, shape decided by the owner (from `birko-framework-44`): one-winner claims become a
  store capability (`IAsyncConditionalUpdateStore<T>.UpdateIfAsync`, on SQL, MongoDB, RavenDB, Cosmos DB, Elasticsearch
  and InMemory; JSON/XML refused); `OAuthClient` and `RefreshTokenRecord` persist as one entity each on every backend, so
  the client store answers predicates and gains `GetByNameAsync` (`OAuthClientNames` can ask it);
  `services.AddOAuthServerStores(IStoreFactory)` registers all five. `Birko.Security.OAuth.Server.SqLite` will be deleted,
  but only after DraCode switches: Birko says when the factory stores land; switch then and tell `birko-framework-44`.
