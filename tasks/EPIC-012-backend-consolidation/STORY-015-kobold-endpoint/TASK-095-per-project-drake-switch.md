---
id: TASK-095
parent: STORY-015
feature: FEATURE-017
status: done
priority: P1
assignee: ai
created: 2026-10-04
depends-on: [TASK-094, TASK-097, TASK-098]
blocks: [TASK-038, TASK-040]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Per-project Drake switch: Drake skips a project whose Drake is off, /kobold project mode still runs it

## Context

`DrakeExecutionService` (`DrakeExecutionService.cs:60-61`) takes every project that is Analyzed/InProgress and Running and
executes all its unassigned tasks; it never checks the project's `drake.enabled` flag. So a project cannot be driven by
hand through `/kobold` project mode — the background Drake takes every new task within a second — which is why the live
sign-off of TASK-038/040 cannot run. Decided 2026-10-04 (owner): use the existing per-project `drake.enabled` flag, managed
from Dragon (and a UI toggle), rather than a new execution state. Depends on TASK-094, which makes Dragon's enable/disable
reach the store the pipeline reads.

## Acceptance criteria

- [x] `DrakeExecutionService` skips projects whose `drake.enabled` is false (logged once per cycle, like Wyvern's "disabled" skip)
- [x] `/kobold` project mode keeps running tasks of such a project
- [x] Turning Drake back on (Dragon `manage_agents` enable) makes Drake pick up the remaining unassigned tasks on its next cycle
- [x] REST toggle for the web UI: `PATCH /api/v1/projects/{id}/agents/{type}` with `{ enabled }` (owner-scoped like the other project endpoints)
- [x] New projects keep Drake enabled by default (TASK-090), so Dragon-driven projects behave as before
- [x] Tests for the skip and the toggle; full suite green
- [x] Live: create a project, switch Drake off, let Wyvern create its tasks, run one through `/kobold` project mode (the TASK-038/040 check), switch Drake back on, watch it finish the rest — then close TASK-038/040

## Out of scope

- A separate "manual" execution state (considered; the per-agent flag was chosen)
- The web UI control itself (TASK-055/056 area) — this task provides the endpoint

## Human test plan

- [x] In Dragon: "turn Drake off for project X" → no tasks start; "turn it back on" → tasks start within ~30 s

## Implementation plan

Logging: the skip logs one Info line per disabled project per cycle (every 30 s), exactly as Wyvern's "disabled" skip does — that is what the criterion asks for.

**Findings.** `DrakeExecutionService.ExecuteCycleAsync` (`:56-62`) picks Analyzed/InProgress + Running projects and never reads the drake flag. No other path bypasses a selection-level skip: `DrakeMonitoringService` only watches stuck Kobolds; `FailureRecoveryJob` / retry paths only reset tasks to `Unassigned`; `DrakeFactory.CreateDrake` is called only by `DrakeExecutionService` and `ProjectRunModeHandler`. `/kobold` project mode (`ProjectRunModeHandler.EnsureRunnable`, `KoboldRunModeHandlers.cs:420`) checks only ids/existence/`ExecutionState == Running` and builds its own Drake — so it shares no gate with the switch. TASK-090 already enables drake on `RegisterProject` (covered by `RegisterProject_ShouldEnable_PipelineAgents("drake")`).

1. **Selection helper** — `public static DrakeCycleSelection SelectProjects(IEnumerable<Project>, Func<string,bool> isDrakeEnabled)` in `DrakeExecutionService`, returning ToProcess / Paused / DrakeDisabled. `ExecuteCycleAsync` uses it with `IsAgentEnabled(id, "drake")` (predicate exceptions → treated as disabled, so a project deleted mid-cycle can't kill the cycle), keeps the paused Debug log, adds `LogInformation("⏭️ Skipping project {ProjectName} - Drake disabled")`. Re-enabling needs no code: the next cycle re-reads the DB flag that Dragon's `manage_agents` writes (TASK-094).
2. **REST toggle** — `PATCH /api/v1/projects/{id}/agents/{type}` in `ResourceEndpoints`, `{ enabled }` (`SetAgentEnabledDto(bool? Enabled)`), `.RequirePermission(ManageProjects)`; 404 when missing or `!ApiOwnership.CanAccess`, 400 on an unknown type (make `AgentConfigurationTool.IsValidAgentType` public so REST and Dragon share one list) or missing `enabled`; calls `ProjectService.SetAgentEnabledAsync`; returns `{ projectId, agentType, enabled }`.
3. **Docs** — the endpoint and Drake-off behaviour in `docs/features/FEATURE-017-*/status.md`.
4. **Tests** — `DrakeExecutionSelectionTests` (temp SQLite + `ProjectService`): disabled → DrakeDisabled; enabled → ToProcess; paused wins over disabled; disable → re-enable via `manage_agents` → back in ToProcess; throwing predicate → skipped. `ProjectRunModeHandlerTests`: `EnsureRunnable` passes with drake disabled. `ResourceEndpointsTests`: owner PATCH false/true flips `IsAgentEnabled`; other user 404; unknown type 400; missing enabled 400; no token 401; view-only token 403. Full suite.
5. **Live** — create a project, Drake off, let Wyvern create tasks, run one through `/kobold` project mode, Drake on, watch it finish the rest; then close TASK-038/040.

**Risks.** A project that already has `drake.enabled=false` while Wyvern is on (Drake ignored the flag until now) will stop silently except for the Info log — check stored flags before deploying (the dev DB currently has no projects). Switching off stops only new starts; running work continues. A project finished entirely via `/kobold` while Drake is off stays InProgress until Drake is back on (completion is checked inside Drake's processing).
## Progress log

- 2026-10-04 — `DrakeExecutionService.SelectProjects` splits candidates into ToProcess / Paused / DrakeDisabled; the cycle logs `⏭️ Skipping project … - Drake disabled` at Info per cycle (as Wyvern does); a predicate that throws counts as disabled. `PATCH /api/v1/projects/{id}/agents/{type}` `{ enabled }` (ManageProjects, owner-scoped 404, 400 on unknown type / missing enabled); `AgentConfigurationTool.IsValidAgentType` made public so REST and Dragon share one list. `/kobold` project mode is untouched (`EnsureRunnable` never read the flag; test pins that). New-project default already covered by TASK-090's test.
- 2026-10-04 — tests: `DrakeExecutionSelectionTests` (4; three proven to fail with the flag ignored), `EnsureRunnable_passes_when_the_projects_Drake_is_switched_off`, five PATCH tests. Full suite: 202 passed. FEATURE-017 ledger: D7 recorded (owner's decision from this task's Context) → TASK-095.
- 2026-10-04 — live check, partial (project `drake-switch-check`, still on the dev server): Drake switched off through Dragon before approval; through Wyrm, two Wyvern analyses and 20+ minutes the cycle logged `⏭️ Skipping project drake-switch-check - Drake disabled` and started nothing. Not completed: Wyvern produced 0 tasks, so there was nothing to run through `/kobold`. Blockers filed: TASK-097 (spec-path lookup — fixed, merged into this branch for the run), TASK-098 (empty Wyvern reply → silent 0-task analysis; now a dependency), TASK-099 (features never passed to Wyvern).
- 2026-10-06 — live check resumed (combined build with TASK-097/098): Wyvern produced 2 tasks (cli-1, doc-1 → cli-1) with Drake off; Drake kept skipping, no Kobold started. `/kobold` project mode ran cli-1: ordered `kobold_run_started` → 30 `kobold_tool_call` / 2 `kobold_reflect` / 2 `kobold_stream` → `kobold_complete` (Done), commit `8675142 feat(python): Implement greet.py CLI greeting script` by Kobold-python on main (no feature branch — TASK-099), no worktree left. Drake switched back on via Dragon (flag 0 → 1): the cycle picks the project up again ("Processing 1 project(s)") but logs "No Drakes created" — the project-mode Drake (`…:kobold-<runId>`) is never removed from `DrakeFactory`, so it holds the project's single Drake slot (`CanCreateDrakeForProject` false). doc-1 not run. Not met yet: criterion 3.
- 2026-10-06 — live check completed (build with TASK-097/098/100): Drake off → failed `/kobold` start on the done cli-1 (`error` frame, Drake released by TASK-100) → Drake on via Dragon at 11:17:12 → at 11:17:20 the cycle logged `🐉 Created Drake drake-switch-check:Documentation` and ran doc-1 → commit `b2040c6 feat(documentation): Write README.md usage documentation`, project → AwaitingVerification. Criteria 3 and 7 and the Dragon test step met.
