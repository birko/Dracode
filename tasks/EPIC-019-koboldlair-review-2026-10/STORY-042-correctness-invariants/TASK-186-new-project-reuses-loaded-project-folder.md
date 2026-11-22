---
id: TASK-186
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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
pr: null
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

- [ ] After Sage loads an existing project in a session, creating a spec under a new name creates its own project folder
      and registers it, proved by a test that fails today
- [ ] An imported project (Seeker registers it, then Sage writes its spec) still gets its spec in the imported folder
- [ ] Creating a spec under the loaded project's own name still updates that project, not a new folder

## Out of scope

- The chat route's LLM context after a switch (TASK-180)

## Human test plan

- [ ] On a throwaway server, home Dragon: ask Sage to load an existing project, then ask for a new project — its card
      appears on the homepage, and the loaded project's spec is unchanged

## Implementation plan

_Populated by `/tasks plan TASK-186` — leave empty until then._
