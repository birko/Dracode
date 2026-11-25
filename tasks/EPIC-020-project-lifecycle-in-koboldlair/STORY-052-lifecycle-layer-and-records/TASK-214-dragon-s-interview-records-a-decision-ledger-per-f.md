---
id: TASK-214
parent: STORY-052
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-211]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Dragon's interview records a decision ledger per feature

## Context

FEATURE-078 D6. Dragon's requirement interview records a decision ledger per feature (approved / deferred /
changed / removed, with reasons); only approved or changed decisions are broken into tasks. It is also where agent
answers (D21) and the person's definitions are recorded.

Decisions: FEATURE-078 D6.

## Acceptance criteria

- [ ] Each feature Dragon captures has a decision ledger with states and reasons
- [ ] Only approved or changed decisions become tasks
- [ ] The ledger is shown in Dragon and the web UI

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] Capture a feature with Dragon, approve one decision and defer another: only the approved one becomes tasks

## Implementation plan

_Populated by `/tasks plan TASK-214` — leave empty until then._
