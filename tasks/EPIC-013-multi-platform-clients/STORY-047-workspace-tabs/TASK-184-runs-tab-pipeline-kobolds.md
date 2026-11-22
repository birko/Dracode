---
id: TASK-184
parent: STORY-047
feature: FEATURE-026
status: todo
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

- [ ] A Kobold Drake summons for a task is registered in `RunRegistry` before it starts, with the project's owner, its
      own mode, the project id and the task id; its status and steps follow its run events like any run
- [ ] The Runs tab lists those runs for their project, named by their task and told apart from CLI and ad-hoc runs
- [ ] Ownership is unchanged: the project's owner sees them, another owner gets 404 for the project, an admin sees all
- [ ] Server tests cover the registration; an e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers the listing,
      red before the change; the suites stay green

## Out of scope

- Keeping runs across a server restart (TASK-185)
- The live run view (TASK-057)

## Human test plan

- [ ] Let a local project run a task: its Kobold run shows in the Runs tab while it works, with its steps moving, and
      ends Completed or Failed with the task

## Implementation plan

_Populated by `/tasks plan TASK-184` — leave empty until then._
