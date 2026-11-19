---
id: TASK-172
parent: STORY-025
feature: FEATURE-028
status: todo
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
