---
id: TASK-207
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-206]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# What the person defines is written into the specification as a new version

## Context

FEATURE-078 D20. When the person defines something the specification lacked, Dragon writes it into the
specification as a new version (specifications already keep versions), so the next question about it is answered from
the specification and Kobolds see the change.

Decisions: FEATURE-078 D20.

## Acceptance criteria

- [x] The person's definition is added to the specification as a new version, with its history — the version is
      bumped and saved; the history tool does not list it (TASK-226: a gap older than this task)
- [x] The same question asked again is answered from the specification without reaching the person
- [x] Running Kobolds see the specification change as they do today (version check)

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] Define something through a question on a throwaway server; the specification's history shows the new version

## Implementation plan

Planned at pick (2026-11-26):

1. Red first: `SpecificationService.AppendDefinitionAsync` adds "- question — answer" under "## Definitions" (created at
   the end when missing, later ones joining it) as a new version; a project without a specification is left alone; and
   the person's answer to a question Dragon passed on reaches Dragon's level and is recorded. The tests did not compile
   before the change.
2. `DragonLevel.OnAnsweredAsync` (answered by the person) calls its `recordDefinition`; the server wires it to
   `AppendDefinitionAsync` for the project. `QuotedAnswerLevel` gets a virtual `OnAnsweredAsync`.
3. `SaveContentAsync` does not mark the specification modified, so a definition triggers no re-analysis; running
   Kobolds see the version change through their specification version check.

## Notes

- **Finding, not fixed:** Dragon's `view_specification_history` answered "Specification 'notes207' not found". The tool
  looks only in Dragon's session's own loaded specifications, and history events are keyed by `Specification.Id`, a new
  GUID on every load that is never saved. So no specification has a history that survives a reload, definitions
  included; an event recorded here would carry yet another id. Recording definitions in the history waits on a stable
  specification id.
- Full .NET suite 695 passed; no client change.
- **Human test** (throwaway server, vLLM `lan2`): project `notes207`, whose spec leaves storage open. Wyvern asked
  "Which storage backend should the data layer use?", path Dragon → person, the card saying the specification does not
  cover it. Answered "2" in Dragon: `specification.md` gained "## Definitions" with "- Which storage backend should the
  data layer use? — One JSON file per note (directory of JSON files)", the version went from 1 to 2, the log said the
  answer was written into the specification, and the re-run planned the project (Analyzed). Dragon quoted the section
  back when asked.
