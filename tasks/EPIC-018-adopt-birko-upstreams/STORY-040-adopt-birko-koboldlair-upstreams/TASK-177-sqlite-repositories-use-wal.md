---
id: TASK-177
parent: STORY-040
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P1
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-17
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-027]
pr: 9
github-issue: null
jira-key: null
---

# KoboldLair's SQLite repositories run on the rollback journal, where a read can silently come back empty

## Context

Found 2026-11-17 while investigating TASK-172 (a test that once saw a summoned Kobold hear no answers).

**Mechanism (Birko's own measurement).** `Birko.Data.SQL.SqLite/Database/Connectors/SqLiteConnector.cs` (TASK-296
remarks): on the rollback journal, a statement on a pooled SQLite handle can be answered from a schema image older
than a `CREATE TABLE` another connection already committed. SQLite re-checks the schema only when it can take a shared
lock; while another connection holds the write lock that re-check fails, so "no such table" stands. Birko reads a
missing table as empty by design (`Birko.Data.SQL/SQL/Connectors/AbstractAsyncConnector.cs`, the
`missingTableIsEmpty` catch in `ReadOnAsync`), so the read returns no rows and no error. Measured in Birko's storm:
rollback journal 7 of 7 runs affected, WAL 0 of 5. WAL is Birko's fix, applied only when the settings are a
`SqLiteSettings` (`SqLiteConnector`, `if (settings is SqLiteSettings …) ApplyJournalMode`).

**DraCode never gets it.** Every `Sql*Repository` in `DraCode.KoboldLair/Data/Repositories/Sql/` passes plain
`PasswordSettings` (`SqlPendingDecisionRepository`, `SqlProjectRepository`, `SqlTaskRepository`, `SqlPlanRepository`,
`SqlHistoryRepository`, `SqlUsageRepository`, `SqlUserRepository`, `SqlProviderConfigRepository`); only the event store
(`Data/LegacyDomainEventMigration.cs`) uses `SqLiteSettings`. Birko caches one connector per file, so the first
repository to build it decides the journal mode for all of them. The owner's real `koboldlair.db` reads rollback
journal (file header bytes 18/19 = 1/1, no `-wal` file), checked 2026-11-17.

**Why it matters.** The server writes one file from many places at once (tasks, plans, usage, events, decisions), which
is exactly the condition above. An answers read that comes back empty makes `Drake.LoadHumanAnswersAsync` return an
empty list rather than null, so `SummonKoboldAsync` resumes a Kobold without the person's answer; any other read can
likewise see "nothing" where rows exist. TASK-172's 30 full-suite runs and 2,200 stress iterations could not hit it,
because that test has no concurrent writer on its file; see TASK-172's notes.

## Acceptance criteria

- [x] A deterministic test shows a repository read on a stale pooled handle, with another connection holding the write
      lock, coming back empty today; with the fix it returns the rows (or throws, never an empty result)
- [x] Every DraCode SQLite repository builds its settings through one helper that returns `SqLiteSettings` (WAL)
- [x] A guard test shows each repository, constructed first on a fresh file, leaves it in WAL
      (`SqLiteConnector.JournalModeInEffect == "wal"` or `PRAGMA journal_mode`), so a future repository cannot turn it
      off by passing plain settings
- [x] `An_unreadable_decision_store_holds_the_task_rather_than_running_it_without_its_answers` and the tests that open
      raw connections to change schema (`TaskAcceptanceAttemptsColumnTests`, `LegacyPlanLoadTests`,
      `EventStoreMigrationTests`, `SqlUsageRepositoryTests`) pass under WAL
- [x] The full suite passes
- [x] `CLAUDE.md` notes that the database runs in WAL, with `-wal`/`-shm` files beside it that a backup must copy too

## Out of scope

- Changing Birko (making WAL the default for plain `PasswordSettings`, or a read that does not treat a missing table as
  empty): a Birko follow-up, raised with the Birko session once this lands.
- Replacing the tests' process-wide `SqliteConnection.ClearAllPools()`: TASK-172 found no effect from it.

## Human test plan

- [x] Start the server against a copy of a real `koboldlair.db` (never the original; Testing environment, scratch
      `ProjectsPath`), and confirm it opens, switches the copy to WAL (`-wal` file appears, header bytes 18/19 = 2/2),
      and that existing projects, tasks and decisions read back intact.

## Notes

- `DraCode.KoboldLair/Data/SqliteDatabaseSettings.cs`: `For(dbPath)` returns `SqLiteSettings` (WAL) and creates the
  directory. All eight `Sql*Repository` classes, the event store (`LegacyDomainEventMigration.CreateStore`) and the OAuth
  stores (`SqliteOAuthStores`) use it; no store on the file passes plain `PasswordSettings` any more.
- `DraCode.KoboldLair.Tests/Data/SqliteJournalModeTests.cs`:
  - On plain settings (the old code path), a pooled handle that loaded the schema before `pending_decisions` existed reads
    it as empty while an unpooled connection holds `BEGIN EXCLUSIVE`; after the lock is released the same read finds
    the row. In WAL, `SqlPendingDecisionRepository.GetUnresolvedForProjectAsync` returns the row in the same setup.
  - The table is copied in from a seed file by an unpooled connection, and the stale handle is the only idle one in the
    pool when the read runs (the store's own handle is held), so the setup does not depend on pool order.
  - Guard: every public class in `Data.Repositories.Sql` with a `dbPath` constructor, found by reflection, built first on
    a fresh file leaves it in WAL; the event store likewise.
  - With the helper's `JournalMode` set to null, the WAL read came back empty and every guard failed (checked, then
    restored).
- Full suite 474/474. The named raw-connection tests pass under WAL unchanged.
- Human test, 2026-11-19: the server (Testing environment, scratch `ProjectsPath`) started on a copy of the real
  `koboldlair.db` (header 1/1 before). After start, `-wal` and `-shm` existed, header 2/2, `PRAGMA journal_mode` = wal,
  every table kept its row count (users 1, usage_records 73, dragon_history 7, `__Events` 10, oauth_clients 80,
  provider_configs 2, provider_models 3, agent_provider_settings 5), `pending_decisions` was created empty, and the log
  had no fail/crit lines. The real database held 0 projects and 0 tasks, so those tables were checked only for being
  readable.
- Birko follow-up (out of scope here): WAL as the default for plain `PasswordSettings`, sent to the Birko framework session 2026-11-19.
