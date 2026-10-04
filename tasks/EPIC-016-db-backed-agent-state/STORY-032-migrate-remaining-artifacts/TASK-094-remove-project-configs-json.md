---
id: TASK-094
parent: STORY-032
feature: null
status: done
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

- [x] The factories read parallel limits from the project repository (falling back to the global defaults); Dragon/Warden `manage_agents` (status, get, enable, disable, set_limit) reads and writes the project repository
- [x] `ProjectConfigurationService` and its JSON file are deleted, with their DI registrations and constructor parameters; nothing reads or writes `project-configs.json`
- [x] Tests: a limit set through `ProjectService` is the one a factory enforces; `manage_agents` enable/disable changes what `IsAgentEnabled` returns
- [x] CLAUDE.md no longer documents the service; full suite green
- [x] Live: on the dev server, disabling and re-enabling an agent through Dragon changes the stored flag the pipeline reads — run 2026-10-04: wyrm.enabled 1 → 0 → 1 in AgentsJson

## Out of scope

- Making Drake respect the flag — TASK-095
- The other file-based artifacts of STORY-032 (wyrm-recommendation.json, analysis.json, planning-context.json, notifications.json)
- Deferred to TASK-096 — the dead `project-config-client.js` calling unserved `/api/project-configs` routes

## Human test plan

- [x] In the web UI, change a project's Kobold max-parallel, then confirm Dragon/Warden reports the same value (and vice versa) — run 2026-10-04 through the web UI's own `/wyvern` commands (`update_project_config` 3 → Dragon reports 3; Dragon `set_limit` 2 → `get_project_config` returns 2); Chrome extension was not connected, so not clicked in the browser

## Implementation plan

**Decided 2026-10-04:** "falling back to the global defaults" means an unknown or empty project id only; a DB project's stored `MaxParallel` always wins. (`AgentConfig.MaxParallel` is a non-nullable `int`, default 1, and new DB projects never copied `KoboldLairConfiguration.Limits` — so a deployment with `Limits.MaxParallelKobolds > 1` gets 1 for projects whose limit was never set. Accepted.)

**Findings.** `IProjectRepository` already has `GetAgentConfig`, `GetMaxParallel`, `IsAgentEnabled` (sync, served from `SqlProjectRepository`'s in-memory cache) and `SetAgentLimitAsync` / `SetAgentEnabledAsync` (async). The factories' sync `CanCreateXForProject` checks can read directly — no sync-over-async. `manage_agents` (`AgentConfigurationTool`) uses `Action<>` setters + `Task.FromResult`, so it needs to become truly async.

1. **Limit resolver** — static `AgentLimitResolver.GetMaxParallel(IProjectRepository?, AgentLimits defaults, string? projectId, string agentType)` in `Factories/`: project's `MaxParallel` when the project exists, else the per-type default (planner → 1).
2. **Factories** — replace the `ProjectConfigurationService` ctor param with `IProjectRepository`: `WyvernFactory`, `WyrmFactory` (defaults from `providerCfg.GetDefaultLimits()`), `KoboldFactory` (drop the `Func<string?,int>` seam), `DrakeFactory` (use its existing `_projectRepository`; stop passing the service to `Drake`).
3. **Drake.cs / ProjectService.cs** — delete the unused field + ctor param; fix the positional `Drake` call in `DrakeFactory`.
4. **manage_agents** — `AgentConfigurationTool` + `WardenAgent` take `Func<…,Task>` setters (or a `ForRepository(IProjectRepository, …)` factory so tests run the same wiring as `DragonService`); `ExecuteAsync` becomes `async`. `DragonService` setters await the repository; `GetProjectAgentConfig` resolves `GetById ?? GetByName` and maps from `project.Agents`.
5. **Program.cs** — drop the service registration/comment; pass `IProjectRepository` into the factory / ProjectService / DragonService registrations (no DI cycle).
6. **Delete** `ProjectConfigurationService.cs` and config-only models (`ProjectConfig`, `ProjectConfigurations`, `ProjectIdentity`, `MetadataConfig`); keep `AgentConfig`/`AgentsConfig`/`SecurityConfig` (used by `Project`).
7. **Docs** — CLAUDE.md § Allowed External Paths, `DraCode.KoboldLair/README.md` tree, `KoboldLairConfiguration.cs:64` comment, `.gitignore` line, CHANGELOG Unreleased. Leave historical docs.
8. **Tests** — fix `ProjectServiceOwnershipTests` setup; add `Services/AgentSettingsDbTests.cs` (temp SQLite): (a) limit set via `ProjectService.SetMaxParallelKoboldsAsync` is enforced by `KoboldFactory.CanCreateKoboldForProject`, and a later change is read live; (b) `manage_agents` disable/enable flips `ProjectService.IsAgentEnabled`, `get` reports the DB value; (c) unknown project falls back to `config.Limits`.
9. `dotnet test DraCode.slnx`; `rg project-configs` hits only history + the dead `/api/project-configs` JS client (→ deferred to TASK-096).
10. **Live** — before deleting `DraCode.KoboldLair.Server/project-configs.json`, compare its "presenter" entry (2 external paths) with the DB row; then disable/re-enable an agent via Dragon and check `AgentsJson` in SQLite.

**Risks.** Dragon `set_limit`/`enable` now actually affect the pipeline and UI; Warden status shows DB values, which may differ from what it showed before. `SetAgent*Async` throws on unknown project — the tool resolves first and catches.
## Progress log

- 2026-10-04 — factories (`Wyvern`/`Drake`/`Wyrm`/`Kobold`) resolve limits via `AgentLimitResolver` → `IProjectRepository.GetMaxParallel`, global `AgentLimits.GetDefaultMaxParallel` only for an unknown/empty project id; `manage_agents` setters are `Func<…,Task>` awaiting `SetAgentEnabledAsync`/`SetAgentLimitAsync`, lookup via `AgentConfigurationTool.ResolveFromRepository`; `ProjectConfigurationService` + `ProjectConfig`/`ProjectConfigurations`/`ProjectIdentity`/`MetadataConfig` deleted; DI + `Drake`/`ProjectService`/`DragonService` params removed.
- 2026-10-04 — `AgentSettingsDbTests` (5 tests) added; the two limit tests proven to fail with the resolver forced to the global default. Full suite: 189 passed.
- 2026-10-04 — dev data: `C:\Source\DraCode-Projects\koboldlair.db` has 0 projects, so the only `project-configs.json` entry ("presenter", limits 1, 2 external paths) is orphaned — nothing to migrate. The gitignored file `DraCode.KoboldLair.Server/project-configs.json` is left on disk (nothing reads it); the `.gitignore` line stays so it is never committed.
- 2026-10-04 — close gate: `manage_agents` resolved projects across every owner (pre-existing, harmless while it wrote a file nothing read; now it writes the real project). Lookup scoped to the session's visible projects (`AgentConfigurationTool.Resolve(GetVisibleProjects(session), …)`); `ManageAgents_ShouldNotChange_AProjectOutsideTheCallersVisibleProjects` added and proven to fail without the scope. Full suite: 190 passed.
- 2026-10-04 — parked at `verify`: the Live criterion and the Human test plan need the dev server (the dev DB has no projects yet).
- 2026-10-04 — the orphaned dev file `DraCode.KoboldLair.Server/project-configs.json` deleted (gitignored, never committed); branch kept local by request.
- 2026-10-04 — live checks on the dev server (throwaway project `task094-check`, 158bf551…): Dragon disable/enable flips `wyrm.enabled` 1 → 0 → 1 in `AgentsJson`; UI `update_project_config` 3 is what Dragon reports. The reverse first FAILED — Dragon `set_limit` threw `Unable to cast JsonElement to IConvertible` (`Convert.ToInt32` on the LLM's `JsonElement` argument; pre-existing, hidden because the unit test passed an `int`). Fixed with a `ParseInt` like `ReflectionTool`'s (number or quoted number); `ManageAgents_SetLimit_ShouldAccept_TheJsonElementTheLlmSends` red before, green after. Re-run: Dragon `set_limit` 2 → UI `get_project_config` returns 2. Full suite: 192 passed.
