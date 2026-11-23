---
id: TASK-173
parent: STORY-028
feature: FEATURE-031
status: done
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
  2026-11-17: passed in all 30 of TASK-172's full-suite runs; TASK-172 found no cause it shares (see its Outcome).

## Acceptance criteria

- [x] A task reopened by orphan recovery, or after its Kobold's run has ended, gets its saved plan revised with the
      unmet criteria, shown by a test
- [x] A test shows the feature's ready-to-merge notice is not raised while one of its tasks waits on a review card
- [x] The flaky test's cause is found and fixed, or a bounded search is recorded as finding nothing

## Out of scope

- The check's rules themselves: TASK-025.

## Human test plan

N/A: each item is proven by automated tests.

## Notes

- **Recovered reopens.** The WrongApproach route (`Drake.HandleEscalationAsync`) took the plan from a live Kobold, and
  fell back to the saved plan only for a parked task. It now falls back to the saved plan whenever no live Kobold holds
  one, which covers orphan recovery and a reopen after the run ended (the Kobold is gone from the factory by then).
  Tests in `TaskAcceptanceGateTests`, with a scripted planner: `A_task_reopened_by_orphan_recovery_gets_its_saved_plan_revised`
  and `A_task_reopened_after_its_Kobold_has_gone_gets_its_saved_plan_revised` — the saved plan keeps its finished step,
  gains the new one, is InProgress at it. Both red without the change (the planner was never asked).
- **Merge hold.** `WyvernFeatureLinkingTests.A_feature_is_not_offered_for_merging_while_one_of_its_tasks_waits_on_a_review_card`
  pins FEATURE-031 D4: a task on `AwaitingHumanDecision` keeps the feature from completing; once Done, the branch is
  offered. It passes on the current code (it pins behaviour); counting `AwaitingHumanDecision` as finished in
  `Wyvern.UpdateFeatureStatus` turns it red, so it can catch the regression.
- **Flaky test: bounded search found nothing.** Both sightings were on TASK-025's uncommitted work in progress, before
  its note fix. Since then: 9 runs (TASK-025), 30 full-suite runs (TASK-172), and here 20 runs of the whole Services
  group (233 tests each, where it was seen) — 59 clean. Reading the path: recovery reaches Done only when
  `PassesAcceptanceAsync` returns true; the workspace is derived from the per-test task file path and the check holds
  no shared or time-based state. Most likely it lived in the work-in-progress code. Left as is.
- **Found on the way, not fixed here:** `CreateImplementationPlanTool` keeps the plan it made in a process-wide static
  (`_lastPlan`), which `KoboldPlannerAgent` reads back after its run. Two planners running at once — two projects, or a
  revision beside a new plan — can take each other's plans. Filed as TASK-195.
- 599/599 .NET; e2e 153/154, the one failure the known flaky `auth.spec.ts` name test (TASK-189).
