---
id: TASK-094
parent: STORY-032
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-10-04
depends-on: []
blocks: [TASK-095]
findings: [FIELD-011]
pr: null
github-issue: null
jira-key: null
---

# Remove project-configs.json — the database is the only store for per-project agent settings

## Context

Found 2026-10-04 while designing a per-project Drake switch. `ProjectConfigurationService` keeps per-project agent settings
in `./project-configs.json`, but every field it stores already lives on the SQL `Project` (same classes: `AgentsConfig`
→ `AgentsJson`, `SecurityConfig` → `SecurityJson`). Nothing syncs the two, and live code reads and writes both:

- **Dragon/Warden `manage_agents` enable/disable** writes the file (`DragonService.cs:609`); the pipeline reads the DB
  (`WyvernProcessingService`, `ProjectService` wyrm gate) — so enabling/disabling agents from Dragon does nothing, and
  Warden can report `Enabled=false` (the file's default for a new entry) while the DB says enabled.
- **Parallel limits:** the UI writes the DB (`ProjectConfigCommandHandler` → `ProjectService`), but `WyvernFactory`,
  `DrakeFactory`, `WyrmFactory` and `KoboldFactory` throttle from the file — UI limit changes are ignored. Dragon's
  `set_limit` writes the file, so the factories honour it but the UI never shows it.
- Dead/inert: `ProjectService` injects the service unused; `Drake` stores it unused; the file's `Security` and
  `Timeout` are never acted on (the DB copies are). `CLAUDE.md` § Allowed External Paths still names the service.
- On the dev machine the only copy is `DraCode.KoboldLair.Server/project-configs.json` (gitignored, last written 2026-03-14).

## Acceptance criteria

- [ ] The factories read parallel limits from the project repository (falling back to the global defaults); Dragon/Warden `manage_agents` (status, get, enable, disable, set_limit) reads and writes the project repository
- [ ] `ProjectConfigurationService` and its JSON file are deleted, with their DI registrations and constructor parameters; nothing reads or writes `project-configs.json`
- [ ] Tests: a limit set through `ProjectService` is the one a factory enforces; `manage_agents` enable/disable changes what `IsAgentEnabled` returns
- [ ] CLAUDE.md no longer documents the service; full suite green
- [ ] Live: on the dev server, disabling and re-enabling an agent through Dragon changes the stored flag the pipeline reads

## Out of scope

- Making Drake respect the flag — TASK-095
- The other file-based artifacts of STORY-032 (wyrm-recommendation.json, analysis.json, planning-context.json, notifications.json)

## Human test plan

- [ ] In the web UI, change a project's Kobold max-parallel, then confirm Dragon/Warden reports the same value (and vice versa)

## Implementation plan

_Populated by `/tasks plan TASK-094` — leave empty until then._