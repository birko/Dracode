---
id: TASK-172
parent: STORY-025
feature: FEATURE-028
status: in-progress
priority: P2
assignee: agent
created: 2026-11-17
depends-on: [TASK-022]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Find why a summoned Kobold sometimes hears no answers in the full test suite

## Context

Found while building TASK-022 (2026-11-16) and filed at its close. In 2 of TASK-022's first 7 full-suite runs,
`EscalationToHumanTests.A_summoned_Kobold_hears_the_tasks_answers_oldest_first_and_not_an_open_question` failed:
the summoned Kobold's `HumanAnswers` was empty, although the answered decisions could be read just before. It never
failed on its own, it passed in the 7 full runs after that, and the read never threw while logged.

The cause matters beyond the test. If production can read an empty list when answers exist, a Kobold resumes without
the person's answer, which is what `Drake.LoadHumanAnswersAsync` returning null is meant to prevent.

Leads, none confirmed:
- Birko reads a missing table as empty by design (`IsMissingTableException`), so a read against a database where
  `pending_decisions` does not exist yet returns nothing, with no error.
- `SqliteConnection.ClearAllPools()` in
  `An_unreadable_decision_store_holds_the_task_rather_than_running_it_without_its_answers` is process-wide, so it can
  affect other test classes that run in parallel.
- Each test class uses its own temp directory. Two repositories pointing at different files could still share
  something static.

## Acceptance criteria

- [ ] The cause is found and named in this file, or a bounded search (say, 30 full-suite runs with logging of the
      database path and the table's row count at the read) is recorded as finding nothing
- [ ] If production code can read an empty list while answers exist, that is fixed and a test fails without the fix
- [ ] If only the tests are at fault, they are fixed so the full suite cannot reproduce it

## Out of scope

- Answer delivery itself: TASK-022.

## Human test plan

N/A: this is a test-suite and storage investigation, and its proof is automated runs.

## Implementation plan

⚠ Acceptance criteria question: AC3 says the tests are fixed "so the full suite cannot reproduce it", but no number
of runs proves "cannot". The bar assumed here is 30 clean full-suite runs **and** 0 hits in the targeted stress loop
(step 2). AC3 also does not say whether it still applies when the cause is in production code and that fix removes the
flake too.

### Leads, ranked by evidence in the code

- **A (strongest, new): no WAL, so a pooled connection reads an old schema, and Birko turns "no such table" into an
  empty read.** Birko's own measurement (`Birko.Data.SQL.SqLite/Database/Connectors/SqLiteConnector.cs`, TASK-296
  remarks): on the rollback journal a pooled handle can answer from a schema image older than a committed
  `CREATE TABLE`; rollback 7 of 7 storm runs affected, WAL 0 of 5. WAL is applied only when the settings are a
  `SqLiteSettings` (`SqLiteConnector.cs` ~229-237). Every DraCode `Sql*Repository` passes plain `PasswordSettings`
  (e.g. `SqlPendingDecisionRepository.cs:43`); only the event store uses `SqLiteSettings`. Connectors are cached per
  file, so the first repository to build one decides for the whole file. A swallowed missing-table read
  (`Birko.Data.SQL/SQL/Connectors/AbstractAsyncConnector.cs` ~372, `missingTableIsEmpty`) makes
  `GetResolvedForTaskAsync` return `[]`, so `Drake.LoadHumanAnswersAsync` returns an empty list, not null, and
  `SummonKoboldAsync` resumes the Kobold without its answers: the production failure this task fears.
- **B (medium): process-wide `SqliteConnection.ClearAllPools()`** in many test teardowns (`EscalationToHumanTests:67`
  and mid-test `:549`, `TaskAcceptanceGateTests:70`, `ParkedTaskDrakeTests:58`, `DecisionDeskTests:56`,
  `DragonDecisionSocketTests:40`, `PendingDecisionsToolTests:35`, several `Data/*` tests), with xUnit running classes
  in parallel (no `xunit.runner.json`). Birko measured it causing cross-class `SQLITE_BUSY` at suite scale
  (`tests/Birko.Data.SQL.SqLite.Tests/ColdTableStormTests.cs`). BUSY is what lets A's stale-schema error stand, so B
  is likely A's trigger rather than a separate cause.
- **C (low): shared static state.** `ResolveLocks` is write-path only, connectors are per path, every test has its own
  GUID directory, DraCode uses no ambient transaction. Logging the read's `DataSource` (step 1) settles it.
- "Missing table read as empty" is how A shows itself, not a separate lead: the table exists from `InitializeAsync`
  and earlier reads in the same test succeed.
- TASK-173's flaky test uses the file-based repositories, so A probably does not apply there; record its result in the
  same loops, fix nothing for it here.

### Steps

1. **Temporary diagnostics, never committed**, gated on `DRACODE_TASK172_DIAG` (a log path in the session scratchpad):
   - in `SqlPendingDecisionRepository.GetResolvedForTaskAsync`: time, thread, full DB path, task id, rows returned;
     then on a `Pooling=False` connection `PRAGMA journal_mode`, whether `pending_decisions` exists in `sqlite_master`,
     and the raw count of resolved rows for the task;
   - in Birko's swallowing catch (`AbstractAsyncConnector.cs` ~372): log message, command and `DataSource` before
     `yield break` (DraCode compiles Birko from source); revert with
     `git -C C:\Source\Birko\Framework checkout -- Birko.Data.SQL/SQL/Connectors/AbstractAsyncConnector.cs`;
   - a time-stamped line at every `ClearAllPools` call in the tests;
   - in the failing test, a `T172-FAIL` line with the answer count and directory before the assertion (~`:501`).
2. **Reproduce, bounded.** Up to 30 runs of the SQL/Services/Agents/Endpoint test namespaces, then 30 full-suite runs
   (`dotnet test DraCode.KoboldLair.Tests --no-build`), recording failed test names per run. Then a temporary
   `Task172StressTests`: the summon scenario 200×, 8 in parallel, each on its own DB, in three variants: (i) as is,
   (ii) with a background `ClearAllPools()` every 1-5 ms, (iii) with the file forced to WAL. Score on the diagnostic
   (empty read while the raw count is above 0), with a positive control that forces a stale-schema read.
3. **Decide from the evidence:**
   - **A confirmed** (swallowed `no such table: pending_decisions` on the test's own file, raw count above 0, journal
     `delete`): production fix (AC2).
     1. Failing test first: `Data/PendingDecisionStaleSchemaTests.cs`, which loads the schema on a pooled connection
        before the table exists, creates and resolves a decision, holds `BEGIN EXCLUSIVE` elsewhere, and asserts that
        `LoadHumanAnswersAsync` never returns an empty list (answers or null). If pooling defeats determinism, the
        stress test becomes the regression test, proved by mutation.
     2. Fix: one helper giving every DraCode SQLite repository `SqLiteSettings` (WAL), used by all eight
        `Sql*Repository` constructors. One repository alone is not enough: the first to build the connector decides.
     3. Mutation proof: the helper returning `PasswordSettings` makes the test fail.
     4. Optional, only if agreed: a read that does not treat a missing table as empty needs a Birko change → its own
        follow-up, not here.
   - **C confirmed** (the swallow's `DataSource` belongs to another test): fix the shared state, with a failing test
     building two repositories on two files at once.
   - **Raw count 0 although resolve said `Resolved`**: a lost write, so investigate `ResolveAsync`/`UpdateAsync`
     with its own failing test.
   - **B triggers it** (failures follow another class's `ClearAllPools`; variant ii reproduces, i does not): the WAL fix
     from A plus the test fix below.
   - **Test fix (AC3), whatever B turns out to be:** replace every process-wide `ClearAllPools()` with a helper that
     clears only this test's pool (`Fixtures/TestSqlite.ReleasePool(dbPath)`, `SqliteConnection.ClearPool` on the
     exact connection string Birko uses); mutation proof: one global clear put back makes variant ii fail again.
   - **Everything clean** (30 full runs + stress): record the bounded search as finding nothing (AC1) with the run
     table; propose the WAL change as its own task only if the owner agrees.
4. **Production exposure, read-only:** read `PRAGMA journal_mode` from a real `koboldlair.db` opened `Mode=ReadOnly`;
   `wal` there means the event store won the connector race on that file, so production is protected only by
   dependency-injection order.
5. **Verify:** 30 full-suite runs with the fix and the diagnostics on, 0 `T172-FAIL` and 0 swallowed reads; stress
   0 of N; full suite; `An_unreadable_decision_store_holds_the_task_rather_than_running_it_without_its_answers` still
   holds under WAL (a leftover `-wal` sidecar must not make the overwritten file readable); the raw-connection schema
   tests (`TaskAcceptanceAttemptsColumnTests`, `LegacyPlanLoadTests`, `EventStoreMigrationTests`,
   `SqlUsageRepositoryTests`) under WAL.
6. **Clean up:** remove the diagnostics, revert Birko (`git -C C:\Source\Birko\Framework status` clean), delete the
   stress class unless it is the regression test, name the cause with evidence here, note TASK-173's results in its
   file.

### Tradeoffs and risks

- WAL adds `-wal`/`-shm` sidecars: a backup copying only `koboldlair.db` misses recent commits, and it does not work
  on network filesystems; existing databases switch on first start. Check `docs/` for backup notes.
- Changing all eight repositories is the wider diff, and the only one that works. A guard test asserting
  `JournalModeInEffect == "wal"` stops a future repository from quietly turning it off.
- Clearing only this test's pool keeps the suite parallel; turning parallelism off would hide A, not fix it.
- At ~2 in 14, 30 clean runs still leave ~1.5% odds of missing a real flake, so the stress test and its positive
  control carry the evidence. The diagnostic's own connections can shift timing.
- Temporary Birko edits must not reach a Framework commit (Birko has unpushed commits and an uncommitted TASK-600
  change of its own to leave alone).
