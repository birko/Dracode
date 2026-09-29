---
id: TASK-080
parent: null
feature: null
# status: todo | in-progress | review (code done, sign-off pending) | blocked | done | cancelled
status: todo
priority: P2
assignee: ai
created: 2026-09-28
depends-on: []
blocks: []
related: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Rename DomainEventEntity.AggregateId to follow Birko's Guid naming rule

## Context

Birko TASK-506 (framework branch `symbio/TASK-819`, not yet on main) renamed every Guid-typed
`…Id` member to `…Guid`. The EventSourcing contract this entity mirrors moved with it:
`IEvent` / `DomainEvent` `AggregateId` → `AggregateGuid`. DraCode followed the framework's
call sites on branch `birko/TASK-506-guid-naming` (commit `a2491d6`) but deliberately left
its **own** members alone, so the persistence entity and the event it maps now disagree:

```csharp
// DraCode.KoboldLair/Data/Repositories/Sql/SqlEventStoreRepository.cs  ToEntity / FromEntity
AggregateId = @event.AggregateGuid.ToString(),
...
aggregateId: Guid.Parse(entity.AggregateId),
```

What is involved:

- `DraCode.KoboldLair/Data/Entities/DomainEventEntity.cs` — `[Table("domain_events")]`,
  `public string AggregateId` with `[RequiredField]` + `[MaxLengthField(36)]`, copied in `CopyTo`.
- `DraCode.KoboldLair/Data/Repositories/Sql/SqlEventStoreRepository.cs` — four read filters
  (`e.AggregateId == aggId`, lines ~74/82/90/98) plus the two mapping sites above. Persisted via
  `AsyncSqLiteModelRepository<DomainEventEntity>` into the server's SQLite file
  (`KoboldLair:Data:SqLitePath`, default `koboldlair.db`).
- **The column name is stored.** Birko derives the SQL column from the property name, so a
  rename changes the `domain_events` schema. An existing `koboldlair.db` keeps a column called
  `AggregateId`, and after the rename the store reads and writes `AggregateGuid`. DraCode has no
  schema-migration mechanism for this table (`Data/Migrations/` holds only `JsonToSqlMigration`,
  the JSON→SQLite import), so this task has to supply the upgrade path.
- **The type is `string`, not `Guid`.** The framework rule is literally about *Guid-typed*
  members. This one stores a Guid as 36-char text and round-trips it through
  `ToString()` / `Guid.Parse`. Whether to also change it to `Guid` is a decision (see plan step 1).
  Changing the type alters the stored representation too, so don't do it without measuring
  what Birko's SQLite provider actually writes for a `Guid`.
- No test covers `SqlEventStoreRepository` or `SpecificationEventService` today.

## Acceptance criteria

- [ ] `DomainEventEntity` exposes `AggregateGuid`. No member, filter or `CopyTo` line still
      references `AggregateId` on the entity.
- [ ] The type decision (keep `string` vs. switch to `Guid`) is written down in this file with
      the reason. If the type changes, the stored format was measured, not assumed.
- [ ] An existing `koboldlair.db` created before the change still loads its events after
      upgrade (rows written under `AggregateId` are readable by aggregate), through an idempotent
      upgrade step that runs before the event store's first use. A fresh database gets
      `AggregateGuid` directly.
- [ ] Regression tests for `SqlEventStoreRepository` against a temp SQLite file: write → read by
      aggregate / up-to-version / from-version / all round-trips, **and** an old-schema database
      (created with an `AggregateId` column and seeded rows) is upgraded and read back correctly.
      The upgrade test is shown to fail without the upgrade step.
- [ ] `dotnet test DraCode.slnx` green.

## Out of scope

- `DomainEventEntity.UserId` (`string?`, same shape and the same stored-column issue). This
  task covers only what was asked for. Whether it follows is its own decision.
- `SpecificationEventRecord.EventId`, `TaskAssignmentMessage.CorrelationId` (string-typed), and
  the `/whoami` JSON key `userId` (a wire contract): DraCode-owned names the framework sweep
  also left alone.
- Any change to the Birko framework.

## Human test plan

- [ ] Take a copy of a real `koboldlair.db` that has events in `domain_events`. Start the
      upgraded server against it and open a specification's history: the pre-upgrade events are
      listed, in version order, with no errors in the log.
- [ ] Start the server a second time against the same file and confirm the upgrade step is a
      no-op (no error, no duplicate work).

## Implementation plan

_Draft — refine with `/tasks plan TASK-080` before starting._

1. Decide the type. Default: keep `string` and rename only, which keeps the stored format
   unchanged so the upgrade is a pure column rename. Record the decision here.
2. Rename the property, `CopyTo`, the four filters and the two mapping sites.
3. Add an idempotent upgrade that runs before the store's lazy init: if `domain_events` exists
   and has an `AggregateId` column but no `AggregateGuid` column, run
   `ALTER TABLE domain_events RENAME COLUMN AggregateId TO AggregateGuid;` (SQLite ≥ 3.25), and
   rename any index that names the old column. Otherwise do nothing.
4. Tests as in the acceptance criteria. Prove the upgrade test goes red with step 3 removed.
5. Land after, or together with, `birko/TASK-506-guid-naming` / Birko `symbio/TASK-819`. This
   task only makes sense once `IEvent.AggregateGuid` exists.
