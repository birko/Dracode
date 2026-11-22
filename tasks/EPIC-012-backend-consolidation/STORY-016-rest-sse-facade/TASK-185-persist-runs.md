---
id: TASK-185
parent: STORY-016
feature: FEATURE-026
status: todo
priority: P3
assignee: agent
created: 2026-11-22
depends-on: [TASK-155]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Runs survive a server restart

## Context

Found at TASK-155's close, where it was out of scope. `RunRegistry` (`DraCode.KoboldLair/Services/RunRegistry.cs`)
keeps every run's record in memory only, so a server restart empties `GET /api/v1/runs`, the Runs tab and
`GET /api/v1/runs/{id}` (a poller then gets 404 for a run that existed). A run still in flight when the server stops
also never reaches a terminal state.

Store the records (run id, owner, mode, project and task, prompt, status, steps, start and end times, summary) in the
SQLite database like the other stores (`SqliteDatabaseSettings.For`, WAL), write them as the folded status changes, and
load them at startup. A run found Pending or Running at startup has no live Kobold behind it any more: mark it Failed
with a reason ("server restarted"). Keep the existing cap and pruning of old terminal runs.

## Acceptance criteria

- [ ] After a server restart, `GET /api/v1/runs` and `GET /api/v1/runs/{id}` return the runs from before it, owner-scoped
      as now
- [ ] A run that was in flight at the restart reads Failed with a reason, not Pending or Running for ever
- [ ] Old terminal runs are still pruned, so the table does not grow without bound
- [ ] Server tests cover the round trip, the in-flight case and pruning; the suite stays green

## Out of scope

- Resuming an interrupted run
- Listing the pipeline's own Kobold runs (TASK-184)

## Human test plan

- [ ] Start an ad-hoc run, let it finish, restart the server: it is still in the Runs tab with its status

## Implementation plan

_Populated by `/tasks plan TASK-185` — leave empty until then._
