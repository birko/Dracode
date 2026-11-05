---
id: TASK-127
parent: STORY-040
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: null
pr: null
github-issue: null
jira-key: null
---

# Use the framework SQL event store; delete SqlEventStoreRepository and DomainEventEntity

## Context

**Unblocked:** Birko TASK-529 landed (2026-10-10): `Birko.Data.EventSourcing.SQL` `SqlEventStore<DB>`, typed `EventStoreConcurrencyException`.

`Data/Repositories/Sql/SqlEventStoreRepository.cs` (141) + `Data/Entities/DomainEventEntity.cs` (72): not atomic, no version uniqueness, version read in memory. `_loose/TASK-080` (rename `AggregateId`) is superseded if the framework owns the entity.

Framework side: Birko TASK-529 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [x] Specification event sourcing writes through the Birko store
- [x] Existing events in `koboldlair.db` are readable (same layout, or a migration)
- [x] `_loose/TASK-080` closed as superseded or done here

## Out of scope

- The framework change itself (Birko TASK-529)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan

Done 2026-11-05.

1. `DraCode.Birko` imports `Birko.Data.EventSourcing.SQL`. `IAsyncEventStore` is registered as
   `SqlEventStore<SqLiteConnector>` on the KoboldLair SQLite database (`LegacyDomainEventMigration.CreateStore`);
   `SpecificationEventService` takes the interface. `SqlEventStoreRepository` and `DomainEventEntity` deleted.
2. `LegacyDomainEventMigration.MigrateAsync`, run in `Program.cs` after `Build()` (before any event can be written):
   reads `domain_events` raw, appends each event through the Birko store (same event id, aggregate, version, type,
   data, metadata, user, time as UTC), then keeps the table as `domain_events_migrated` — renamed, or merged into it
   when one exists. On a version already in the store: same event id → skipped (interrupted run); a different id →
   appended at the aggregate's next version, logged — the case of a pre-TASK-127 server run after a migration, which
   numbers from its own recreated, empty table.
3. `SpecificationEventService`: the six `Record*` methods go through `AppendNextAsync`, which re-reads the version and
   retries on `EventStoreConcurrencyException` (up to 5 attempts). Before, two concurrent writes could both store the
   same version; now the unique index rejects one, and the retry lands it on the next.
4. Both event-store spots in `Program.cs` set `DataStorageConfig.ProjectsPath` themselves, instead of relying on
   another registration having mutated the shared options first (as the old registration did).

## Close notes

- Closed 2026-11-05. Suite 296/296 (`EventStoreMigrationTests`, 6).
- Fixture: a `domain_events` dump written by the pre-migration `SqlEventStoreRepository` at a502718 (layout and text
  datetime format identical to the real `koboldlair.db`). Red: the copy disabled → the two migration tests; the
  service retry disabled → the concurrent-writers test (`EventStoreConcurrencyException`); conflicts always skipped
  (the first version of the migration) → the "old server ran after a migration" test loses the event.
- Real data: the 10 events (8 aggregates) of `C:/Source/DraCode-Projects/koboldlair.db` — on a copy, all moved with
  identical ids, versions, types, times and payloads.
- ⚠ A server start meant for a scratch copy ran against the real `koboldlair.db`: `appsettings.local.json` is loaded
  after environment variables and set `ProjectsPath` back. The migration ran there correctly (verified per event:
  ids, versions and times match; dump diff shows only the event tables changed). Restoring the pre-run snapshot was
  refused by the permission check and left to the user.
- Behaviour change: a version race on one specification now retries instead of storing a duplicate version.
- `_loose/TASK-080` (now STORY-044) closed as superseded.
- Human test plan: N/A (per task).
