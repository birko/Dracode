---
id: TASK-224
parent: STORY-054
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# A scheduled CI check opens an issue when a mirrored lifecycle skill changes

## Context

FEATURE-078 D28. KoboldLair mirrors DraCode's lifecycle skills (C:\Source\project-lifecycle-skills). A mapping
file lists which skill files each KoboldLair agent or tool mirrors and the skills version each was last reviewed
against. A scheduled GitHub Actions job compares them with the skills' latest version and opens or updates one issue
listing the changed files and their changes since the last review. Marking an item reviewed moves its version forward.

Decisions: FEATURE-078 D28.

## Acceptance criteria

- [ ] A mapping file lists each mirrored skill file, its KoboldLair counterpart and the reviewed version
- [ ] A scheduled workflow opens one issue, or updates the open one, when a mapped file changed
- [ ] No change, no issue; the workflow can also be run by hand

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] Run the workflow by hand after a change to a mapped skill file: one issue lists it

## Implementation plan

_Populated by `/tasks plan TASK-224` — leave empty until then._
