---
id: TASK-186
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-033]
pr: 34
github-issue: null
jira-key: null
---

# A new project cannot be created once Dragon has loaded another project in the session

## Context

Found 2026-11-22 in TASK-149's human test. In a home Dragon chat, the person asked about waver and waver2 (Sage loaded
both), then asked for a new project `todo149`. Every create failed: Sage's `manage_specification` answered "already
exists, use update" for `todo149` and for names never used before (`todo149-final`, `todo149-final2`), while `load` and
`list` said they did not exist.

Cause: `DragonService.ResolveProjectFolder` (Sage's `getProjectFolder`) returns `session.CurrentProjectFolder` whenever
it is set and exists — meant for an import, where Seeker registers the project and Sage then writes its spec there.
After Sage loads a project, that folder is the loaded project's, so a create under any name resolves to waver2's folder,
which already holds a `specification.md`. Not new with TASK-149; the project dock's chat has the same path, but the
home Dragon is where people start projects, so it shows there first.

## Acceptance criteria

- [x] After Sage loads an existing project in a session, creating a spec under a new name creates its own project folder
      and registers it, proved by a test that fails today
- [x] An imported project (Seeker registers it, then Sage writes its spec) still gets its spec in the imported folder
- [x] Creating a spec under the loaded project's own name still updates that project, not a new folder

## Out of scope

- The chat route's LLM context after a switch (TASK-180)

## Human test plan

- [x] On a throwaway server, home Dragon: ask Sage to load an existing project, then ask for a new project — its card
      appears on the homepage, and the loaded project's spec is unchanged
  - **Passed 2026-11-22:** see Notes

## Implementation plan

Sage's `create` (`SpecificationManagementTool.CreateSpecificationAsync`) takes its folder from `getProjectFolder`,
which is `DragonService.ResolveProjectFolder`, and refuses when that folder already has a `specification.md`.

1. `ResolveProjectFolder(session, name)` reuses `session.CurrentProjectFolder` only when that folder is meant for this
   spec: it holds no `specification.md` yet (an imported project waiting for its spec), or the project registered there
   is named `name` (same sanitized name). Otherwise it goes on as today: an existing project by name, else a new folder.
2. Make it `internal` (the Server already exposes internals to the tests since TASK-149) and test it directly:
   a loaded project with a spec + a new name → a new folder under `ProjectsPath`, the loaded spec untouched (red today);
   an imported folder with no spec → that folder; the loaded project's own name → its folder (so `create` still answers
   "use update").
3. Human test on a throwaway server: home Dragon, have Sage load waver, then ask for a new project.

Not changed: `FindProjectByName`'s containment match (a new name that contains an existing project's name resolves to
that project) — pre-existing, outside this task.

## Notes

- **Fix** (`DragonService.ResolveProjectFolder`, now `internal`): the session's folder is reused only when it is meant
  for the spec being created (`IsFolderFor`): it has no `specification.md` yet (an imported project waiting for its
  spec), or the project registered there has the same sanitized name. Otherwise resolution goes on as before (a project
  by name, else a new folder).
- **Tests:** `DragonProjectSocketTests` +2: after a project with a spec is loaded, a new name resolves to its own new
  folder and the loaded spec is untouched, while the loaded project's own name still resolves to it (red with the old
  rule: it resolved to the loaded `shop` folder); an imported folder with no spec is reused (guards the import path).
  Suites .NET 535/535, e2e 136/136. The first e2e run after a cold client-host start had one failure, and three runs
  after it were green, so it was a flake (its name was not captured).
- **Human test** (throwaway Testing server, scratch copy of `projects160`, LAN model): in the home Dragon, Sage loaded
  waver and summarised it, then `notes186` was asked for. Its folder was created and registered, the card appeared on
  the homepage, waver's `specification.md` kept the same hash, and the home conversation was stored complete.
