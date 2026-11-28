---
id: TASK-228
parent: EPIC-016
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-26
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-200]
blocks: [TASK-229, TASK-230]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Stores work on any database (3/4): OAuth stores, service keys and the event store on the store factory

## Context

The third slice of TASK-200 (split 2026-11-26). Decided in the grill: the OAuth and event stores use the same database
and setting as every other store. Today `SqliteOAuthStores`, `ServiceKeyRegistry`, `OAuthClientNames` and the
specification event store (Birko.Data.EventSourcing.SQL, with `LegacyDomainEventMigration`) are built against SQLite.

Event sourcing on NoSQL backends (MongoDB at least) does not exist in Birko; filed as Birko TASK-995 (P2, todo) by `birko-framework-44` on
2026-11-26 (the owner's call). Until it lands, the event store works on SQL backends only.

## Acceptance criteria

- [x] The service-key registry is built from the store factory. ~~The OAuth stores and OAuth client names~~ moved to
      TASK-230 (2026-11-28, the owner's call): Birko has no factory-built OAuth stores yet (Birko TASK-1013)
- [x] The specification event store comes from the configured backend: SQL backends through Birko's SQL event store,
      MongoDB through Birko's native MongoDB one (Birko TASK-995 landed, so NoSQL is available now), anything else
      through Birko's generic one
- [x] ~~`LegacyDomainEventMigration` runs only on SQLite~~ removed outright (2026-11-28, the owner's call: the old
      event history can be thrown away)
- [x] The concurrency test (two writers on one specification land on distinct versions) passes

## Human test plan

- [x] Sign in with an API key and change a specification on a throwaway server: both work as before on SQLite

## Implementation plan

1. Red: `EventStoreTests` (SQLite gives Birko's `SqlEventStore` on `__Events`; InMemory answers too; two writers),
   `ServiceKeyRegistryTests` (SQLite and InMemory) — both failed to compile against the old API.
2. `DataStores.EventStore(factory)` = `factory.GetEventStore(o => o.AddSql().AddMongoDB())`; DraCode.Birko imports
   `Birko.Data.EventSourcing.MongoDB`.
3. `ServiceKeyRegistry(IStoreFactory)` over `StoreRepository<ServiceKeyEntity>`; the in-memory fallback is gone (the
   InMemory backend replaces it).
4. Program.cs: the registry and the event store from `RepositoryFactory.StoreFactoryFor`; the startup migration block,
   `LegacyDomainEventMigration`, its tests and the `domain_events` fixture removed.
5. Found by the MongoDB human test: `SpecificationEventService` gave up after 5 conflicting attempts. Now
   `MaxAppendAttempts` = 32 with a 1–50 ms random pause; `Many_writers_at_once_on_one_specification_all_land` (20 writers
   on InMemory) fails 3/3 at 5 and passes at 32.
6. Split: TASK-230 for the OAuth stores and `OAuthClientNames`; Birko TASK-1013 asked of `birko-framework-44`.

## Notes

- 2026-11-28: done. Full suite 756 passed (the six migration tests gone; four event-store and three key-registry tests new). Human tests:
  - MongoDB (the live `birko-live-mongo-standalone` container, a fresh database dropped afterwards): two services on two
    factories, one specification created and ten features added at once → versions 1..11 and a `MongoDBEventStore`.
    The first run failed (a writer lost the race five times, `EventStoreConcurrencyException` at version 6) — the store
    refused correctly; the service's retry limit was the bug (plan step 5). Then 5/5 runs passed.
  - Throwaway server (Testing, scratch `koboldlair-test.db`, my own random JWT secret): a user created a key (201, listed
    in its keys, a `service_keys` row in the database), got a token with it (200), created a project and changed its
    specification as the key (201, 200 → version 2), revoked it, and the next token was refused (400). The REST
    specification route records no events (only Dragon's tools do), so the event store was not exercised here — the
    automated tests and the MongoDB run cover it.
  - The flaky `Two_writers…` test seen in TASK-220 was most likely this same retry limit.

- 2026-11-27: the owner confirmed that existing event history (`__Events`, the real `koboldlair.db` included) can be
  thrown away, so this task needs no migration step. Birko TASK-995 (decided in Birko commit 47f99a87): one generic
  event store over any `IStoreFactory`, called as `factory.GetEventStore(o => o.AddSql())`, with the overrides
  registered once at startup. SQL factories still answer `SqlEventStore` on the same `__Events` table.
