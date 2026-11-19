---
id: TASK-171
parent: STORY-025
feature: FEATURE-028
status: todo
priority: P3
assignee: agent
created: 2026-11-16
depends-on: [TASK-021]
blocks: []
findings: []
pr: null
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

- [ ] A question in parallel mode stops every batch Kobold of the task without losing the asker's plan log entry or checkpoint, and is not logged as a failure
- [ ] A question in a turn that also completes the plan, or on the last iteration, leaves the task resumable without redoing finished work
- [ ] Tests for each case

## Out of scope

- Delivering the answer on resume (TASK-022)

## Human test plan

N/A — run-ending paths, asserted with the scripted-provider harness.

## Implementation plan

_Populated by `/tasks plan TASK-171` — leave empty until then._
