---
id: TASK-226
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-26
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-045]
pr: null
github-issue: null
jira-key: null
---

# A specification's history is lost on every reload: its id is never saved

## Context

Found 2026-11-26 in TASK-207's human test. Asked for a project's specification history, Dragon's
`view_specification_history` answered "Specification 'notes207' not found", although the specification loads fine.

- `Specification.Id` (`DraCode.KoboldLair/Models/Projects/Specification.cs`) is a new GUID every time a `Specification`
  is constructed, and `SpecificationService` never saves or restores it (`specification.features.json` carries the
  version and hash, not the id).
- History events (`SpecificationEventService`, the Birko event store) are keyed by that id, so the events recorded in one
  Dragon session belong to an id no later load has: no specification keeps a history across a reload or a restart.
- `SpecificationHistoryTool` looks the specification up only in Dragon's session's own loaded specifications, so one
  this session did not load is "not found".
- TASK-207 writes what the person defines into the specification as a new version; that version is saved but not in
  the history, for the reasons above.

## Acceptance criteria

- [ ] A specification's id is stable: saved with the specification and the same on every load (an existing project gets
      one on first load, kept from then on); a test pins it, red today
- [ ] The history tool finds a project's specification whether or not this session loaded it, and lists its events
- [ ] A specification change made outside Dragon's chat — the REST update, a definition (TASK-207) — is recorded in the
      history like one Dragon makes
- [ ] History recorded before the change: either still found (a stated migration) or stated as lost

## Human test plan

- [ ] On a throwaway server, change a specification through Dragon, restart, and ask Dragon for the history: both
      versions are listed; answer a "defines it" question and the definition appears as a version too

## Implementation plan

_Populated by `/tasks plan TASK-226` — leave empty until then._
