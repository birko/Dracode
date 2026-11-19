---
id: TASK-173
parent: STORY-028
feature: FEATURE-031
status: todo
priority: P2
assignee: agent
created: 2026-11-17
depends-on: [TASK-025]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Acceptance check follow-ups: recovered reopens, the merge hold, a flaky test

## Context

Found while building TASK-025 and filed at its close (2026-11-17). A grouped task: three small loose ends of the
acceptance check that share its code (`Drake.PassesAcceptanceAsync`, `ReopenOrFailAsync`, `RecoverOrphanedTasksAsync`)
and its test class.

- **A recovered task's reopen does not revise its plan.** Orphan recovery runs the check without a Kobold, and the
  WrongApproach route revises a plan only from a live Kobold or a parked task's saved plan. So the reopened task reruns
  a finished plan unchanged, finishes at once, fails again, and reaches the cap and a fix task without anyone trying
  the fix. Load the saved plan for the reopen too.
  Seen 2026-11-19 on a live run too (TASK-025's fail-side test): the WrongApproach route looks the Kobold up through
  the factory after its run has ended, finds none, and logs "No active plan to revise"; the next Kobold discarded the
  stale checkpoint and reran the old plan from step 0. So the saved plan is needed whenever no live Kobold holds one,
  not only after orphan recovery.
- **The feature's "ready to merge" notice waiting for a reviewed task has no test.** It follows from
  `Wyvern.UpdateFeatureStatus` counting only Done tasks; a test should pin it so a change there cannot quietly offer a
  branch for merging while a review card is open (FEATURE-031 D4).
- **A flaky test.** `TaskAcceptanceGateTests.A_task_recovered_with_a_finished_plan_takes_the_same_check_rather_than_going_straight_to_done`
  ended Done in 2 runs alongside the other service tests, never alone, and passed in 9 since; see TASK-025's notes.
  Possibly related to TASK-172's flake (same suite, same timing).

## Acceptance criteria

- [ ] A task reopened by orphan recovery, or after its Kobold's run has ended, gets its saved plan revised with the
      unmet criteria, shown by a test
- [ ] A test shows the feature's ready-to-merge notice is not raised while one of its tasks waits on a review card
- [ ] The flaky test's cause is found and fixed, or a bounded search is recorded as finding nothing

## Out of scope

- The check's rules themselves: TASK-025.

## Human test plan

N/A: each item is proven by automated tests.
