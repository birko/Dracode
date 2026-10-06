---
id: TASK-100
parent: STORY-015
feature: FEATURE-017
status: in-progress
priority: P1
assignee: ai
created: 2026-10-06
depends-on: []
blocks: [TASK-095, TASK-040]
findings: [FIELD-015]
pr: null
github-issue: null
jira-key: null
---

# /kobold project mode never releases its Drake, so the project's background Drake is locked out afterwards

## Context

Found 2026-10-06 during TASK-095's live check. `ProjectRunModeHandler.StartAsync` (`KoboldRunModeHandlers.cs:~373`)
registers a Drake in `DrakeFactory` named `{project}:{area}:kobold-{runId}` and runs the task in the background, but never
calls `DrakeFactory.RemoveDrake` — not after the run, and not when it throws before starting (task not unassigned/ready).
`DrakeFactory.GetActiveDrakeCountForProject` counts that Drake for good, and with the default per-project Drake limit of 1
`CanCreateDrakeForProject` stays false: after one `/kobold` project run, `DrakeExecutionService` cannot create a Drake for
any area of that project until the server restarts. It logs only "No Drakes created … no task files found or all areas
complete/blocked" (the limit skip is Debug-level).

Measured on `drake-switch-check`: cli-1 run through `/kobold` (Done, committed), Drake switched back on → every cycle
"Processing 1 project(s)" then "No Drakes created"; doc-1 (dependency met) never started. Found while signing off TASK-040
(project mode), whose criterion "respects per-project parallel … limits" this breaks from the other side.

## Acceptance criteria

- [x] The project-mode Drake is removed from `DrakeFactory` when its run ends — success, failure, or a Kobold that could not be summoned
- [x] It is also removed when `StartAsync` throws after creating it (task not unassigned/ready)
- [x] Regression test: after a project-mode start that fails on a non-ready task, `GetActiveDrakeCountForProject` is back to 0; proven to fail before the fix
- [x] Full suite green
- [ ] Live: after a `/kobold` project run, the background Drake (Drake on) picks up the project's remaining tasks on its next cycle

## Out of scope

- The Debug-level "Drake limit" skip message — leave as is; this fixes the leak behind it
- Features never reaching Wyvern — TASK-099

## Human test plan

- [ ] Run one task through `/kobold` project mode, then (Drake on) watch the background Drake start the remaining task within ~30 s

## Implementation plan

1. In `ProjectRunModeHandler.StartAsync`, wrap everything after `CreateDrake` so a throw before the background run removes the Drake (`_drakeFactory.RemoveDrake(drakeName)`) and rethrows.
2. In the background `Task.Run`, remove the Drake in a `finally` after `ExecuteTaskAsync` (covers success, the null "no Kobold" result, and exceptions).
3. Regression test in `ProjectRunModeHandlerTests`: real `DrakeFactory` + temp SQLite project whose task file holds one Done task; `StartAsync` for it throws; assert the project's active Drake count is 0. Red before step 1.

## Progress log

- 2026-10-06 — `ProjectRunModeDrakeReleaseTests` written first: red for the right reason only after giving the test a local `ollama` provider (a Drake cannot be built without one) — "Expected … GetActiveDrakeCountForProject … to be 0, but found 1". Fix: `RemoveDrake` in a `catch` around the post-create setup and in a `finally` of the background run. Green; full suite 196 passed. The success-path `finally` is not unit-tested (needs a live Kobold).
- 2026-10-06 — live (dev server): with Drake off, a `/kobold` project start on the done cli-1 returned its `error` frame; Drake switched on → 8 s later the background Drake created `drake-switch-check:Documentation` and ran doc-1 to its commit. Before the fix, the same sequence (after cli-1's successful run) logged "No Drakes created" every cycle. The live path exercised is the failed-start release; the success-path `finally` is the same `RemoveDrake` and is not separately shown live.
