---
id: TASK-171
parent: STORY-025
feature: FEATURE-028
status: done
priority: P3
assignee: agent
created: 2026-11-16
depends-on: [TASK-021]
blocks: []
findings: []
pr: 51
github-issue: null
jira-key: null
---

# ask_human: runs that end some other way as they ask

## Context

Found at TASK-021's close review (2026-11-16). A grouped task: each case is about how a Kobold run ends at the moment
it asks a person, and they share the code — the turn-boundary stop in `Kobold.RunHooks.OnIterationStartingAsync`
and the plan paths' cancellation handling in `Kobold.cs`.

- **Parallel execution** (`Planning.ExecutionMode = "parallel"`): batch Kobolds share the task's run token
  (`Drake.ExecuteTaskParallelAsync`). A question parks the task; when the asking Kobold's run stops, its siblings are
  cancelled mid-turn, not at their turn boundary. Each sibling saved its own copy of the plan, and their shutdown
  saves can overwrite the asker's `ask_human` log entry and checkpoint. The next batch's `semaphore.WaitAsync` then
  throws and is logged as a failure.
- **The asking turn also finishes the plan** (the enhanced path's step detection stops the run before the stop check)
  **or uses the last allowed iteration:** the run ends normally, the Kobold may read Done, the task stays parked (the
  sync guard), no completion commit happens, and after the answer the plan runs again from its saved state.

## Acceptance criteria

- [x] A question in parallel mode stops every batch Kobold of the task without losing the asker's plan log entry or checkpoint, and is not logged as a failure
- [x] A question in a turn that also completes the plan, or on the last iteration, leaves the task resumable without redoing finished work
- [x] Tests for each case

## Out of scope

- Delivering the answer on resume (TASK-022)

## Human test plan

N/A — run-ending paths, asserted with the scripted-provider harness.

## Implementation plan

Decided with the owner 2026-11-24 as FEATURE-028 D10 (a full fix in parallel mode) and D11 (a question wins over the
run's end).

1. Red first. `KoboldRunHooksTests` gets four tests: a question in the turn that finishes the plan, a question on the
   last iteration, a run stopped by its task being parked elsewhere, and two parallel batches (the asker keeps its log
   entry and checkpoint, the sibling keeps its finished step, only the asker's batch resumes the checkpoint).
   `ParallelQuestionTests` drives `Drake.ExecuteTaskParallelAsync` through a test-only `StepBatchRunner`: no group
   starts after a batch asks, and a cancelled run ends without throwing. All 7 failed before the fix.
2. `Kobold`:
   - `IsTaskParked` is set by Drake; `WaitsOnPerson` = this run asked or the task is parked.
   - `RunHooks` stops on `WaitsOnPerson` ahead of "all plan steps complete".
   - After a last-iteration question, `RunPlanStepsAsync` saves the plan and the conversation, and the basic path saves
     the plan.
   - The enhanced path skips the finish bookkeeping when the run stopped for a person (no Done, plan not Completed).
   - A position just past the last step is no longer reset to step 0, and the resume message says every step is done.
3. Parallel batches:
   - A batch's plan copy carries `BatchSteps`.
   - `KoboldPlanService.SavePlanAsync` merges such a copy into the stored plan under a per-task lock: its own steps,
     log entries the store lacks, step assignments, and a failure; the cursor is recomputed. A debounced save of a
     copy is immediate.
   - Batches save no step checkpoints; the asker's checkpoint records its `BatchSteps`, and only a run with equal
     `BatchSteps` resumes it.
4. `Drake`:
   - The group loop starts no batch or group once the task is parked or its run cancelled, and ends quietly.
   - Batches no longer take the token in `Task.Run`.
   - The sequential path reports a parked stop instead of "reached iteration limit".
5. CLAUDE.md's ask_human paragraph describes D10 and D11.

## Notes

- The description at filing said siblings were cancelled mid-turn. Since TASK-021 a question parks without cancelling,
  so in fact the siblings ran on to the end and later groups started for the parked task. The fix covers both.
- A group never has more batches than `MaxParallelSteps` (the splitter sizes batches to it), so within a group every
  batch starts at once. The park check that matters is between groups.
- Fixed in passing: the group loop reloaded the plan with its arguments swapped (filename and project for project and
  task), so the reload never found it.
- Full .NET suite: 607 passed. The e2e suite was not run: nothing the web client reaches changed. No human test: the
  plan says run-ending paths are asserted with the scripted-provider harness, and parallel mode is off by default.
