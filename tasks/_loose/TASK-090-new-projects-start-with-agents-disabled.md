---
id: TASK-090
parent: null
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-03
depends-on: []
blocks: [TASK-086]
findings: [FIELD-007]
pr: null
github-issue: null
jira-key: null
---

# A project created while the server runs is never analyzed: its agents start disabled

## Context

Found 2026-10-03 while working TASK-086. `ProjectService.RegisterProject` (used by Dragon and by `POST /api/v1/projects`)
creates a project whose agents are all `enabled: false` (`AgentConfig.Enabled` defaults to false). Only
`InitializeProjectConfigurationsAsync`, which runs **once at server startup**, enables them. So a project created
while the server runs reaches `WyrmAssigned` and then `WyvernProcessingService` logs "Skipping analysis … Wyvern
disabled" forever, until the server restarts. The startup pass also writes provider **names** into the project
(`GetProviderForAgent`), which is what triggered TASK-089.

## Acceptance criteria

- [x] `RegisterProject` enables wyrm, wyvern, drake, koboldPlanner and kobold, leaving provider null (global default)
- [x] A test registers a project and reads every agent back as enabled with a null provider (fails without the fix) — `RegisterProject_ShouldEnable_PipelineAgents`: 5/5 red without the fix
- [x] Live: a project created and approved on a running server reaches Analyzed without a restart — 2026-10-03: Analyzed → InProgress within 60 s of approval
- [x] Full test suite green — 180/180

## Out of scope

- Changing what the startup pass does for existing projects (it only touches projects that have nothing enabled)

## Human test plan

N/A — criterion 3 is the live check.

## Implementation plan

Not drafted — set the five flags in `RegisterProject` before `Add`.