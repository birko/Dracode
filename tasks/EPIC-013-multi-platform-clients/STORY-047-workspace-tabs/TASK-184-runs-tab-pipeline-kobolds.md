---
id: TASK-184
parent: STORY-047
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-22
depends-on: [TASK-155]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Runs tab: list the pipeline's own Kobold runs

## Context

Found at TASK-155's close. The Runs tab lists what `RunRegistry` knows, and only the two transports register there:
`/kobold` (the CLI) and `POST /api/v1/runs`. The Kobolds Drake summons for a project's tasks (`Drake.SummonKoboldAsync`,
`Orchestrators/Drake.cs`) never appear, though they do most of the work, so the tab is empty for a project that is
running.

They already publish to the same `KoboldRunEventSource` (`KoboldFactory.CreateKobold` sets it), each with its own run
id (`Kobold.RunId`, or one given through `AssignRunId`). What is missing is the registration: assign the run id before
the Kobold starts (subscribe-before-start, as the transports do) and `RunRegistry.Register` it with the project's owner,
a mode such as `pipeline`, the project id and the task id. Then `GET /api/v1/runs?projectId=` and the Runs tab list
them with no further change, and TASK-057's live view can open them like any run.

Decisions: FEATURE-026 D7, D20.

## Acceptance criteria

- [x] A Kobold Drake summons for a task is registered in `RunRegistry` before it starts, with the project's owner, its
      own mode, the project id and the task id; its status and steps follow its run events like any run
- [x] The Runs tab lists those runs for their project, named by their task and told apart from CLI and ad-hoc runs
- [x] Ownership is unchanged: the project's owner sees them, another owner gets 404 for the project, an admin sees all
- [x] Server tests cover the registration; an e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers the listing,
      red before the change; the suites stay green

## Out of scope

- Keeping runs across a server restart (TASK-185)
- The live run view (TASK-057)

## Human test plan

- [x] Let a local project run a task: its Kobold run shows in the Runs tab while it works, with its steps moving, and
      ends Completed or Failed with the task

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Registered from the event stream, not in Drake:** a Kobold's run can start on several paths (basic and enhanced
  plan, parallel steps with a Kobold each, no plan), so registering at summon time would miss some. Instead
  `KoboldRunEventSource.RunStartedUnwatched` is called, synchronously and before delivery, when a run starts that
  nobody subscribed to, and `RunRegistry.TrackUnregisteredRuns` registers it there as a `pipeline` run with the project
  and task the run's events carry (`Kobold.PublishRunEvent` stamps them) and the project's owner — so it receives
  that start event and everything after it. Runs a transport registered first are left as they were. `Program.cs`
  creates the registry, wired to the project repository for owners, whenever the Kobold factory is created.
- **The no-plan path now has telemetry:** `Kobold.StartWorkingAsync` publishes a start and, in its `finally`, the same
  end as the plan paths through one helper (`EndRunTelemetry`); a failed run's error event carries the Kobold's error
  message when it has one, not only "Run ended with status Failed".
- **Runs tab:** a run's origin as a badge — "Pipeline", "Started by hand" (a project run over the CLI or API), or
  "Ad-hoc"; the empty message names the agents' runs. Parallel step execution lists one run per step Kobold.
- **Tests:** .NET `PipelineRunsTests` (no-plan and plan path registered with project, task, owner and ending
  Completed; a transport's run keeps its mode and owner; without tracking nothing is added) — the no-plan one red with
  the old `Kobold`, the rest not compiling against the old registry. e2e `runs.spec.ts` gains a pipeline-run test (red
  on the old tab). Suites .NET 520/520, e2e 113/113.
- **Human test** (throwaway Testing server on a scratch copy, JWT on, LAN model): on `waver2`'s Runs tab, retrying its
  failed task made Drake run it; its run showed as "[impl-1] Implement waver.js…", Pipeline, Running, then Completed
  with 2 of 2 steps, and the task was Done. No console errors.
