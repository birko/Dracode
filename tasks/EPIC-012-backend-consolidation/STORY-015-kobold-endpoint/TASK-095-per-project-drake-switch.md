---
id: TASK-095
parent: STORY-015
feature: FEATURE-017
status: todo
priority: P1
assignee: ai
created: 2026-10-04
depends-on: [TASK-094]
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

- [ ] `DrakeExecutionService` skips projects whose `drake.enabled` is false (logged once per cycle, like Wyvern's "disabled" skip)
- [ ] `/kobold` project mode keeps running tasks of such a project
- [ ] Turning Drake back on (Dragon `manage_agents` enable) makes Drake pick up the remaining unassigned tasks on its next cycle
- [ ] REST toggle for the web UI: `PATCH /api/v1/projects/{id}/agents/{type}` with `{ enabled }` (owner-scoped like the other project endpoints)
- [ ] New projects keep Drake enabled by default (TASK-090), so Dragon-driven projects behave as before
- [ ] Tests for the skip and the toggle; full suite green
- [ ] Live: create a project, switch Drake off, let Wyvern create its tasks, run one through `/kobold` project mode (the TASK-038/040 check), switch Drake back on, watch it finish the rest — then close TASK-038/040

## Out of scope

- A separate "manual" execution state (considered; the per-agent flag was chosen)
- The web UI control itself (TASK-055/056 area) — this task provides the endpoint

## Human test plan

- [ ] In Dragon: "turn Drake off for project X" → no tasks start; "turn it back on" → tasks start within ~30 s

## Implementation plan

_Populated by `/tasks plan TASK-095` — leave empty until then._