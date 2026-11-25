---
id: TASK-209
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-201]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Questions reach the person in rounds of at most five, each with a recommended answer

## Context

FEATURE-078 D22. Questions that reach the person come in rounds of at most five, each with a recommended answer and
its one-line reason, so agreeing is one word.

Decisions: FEATURE-078 D22.

## Acceptance criteria

- [ ] Every question that reaches the person carries a recommended answer and its reason
- [ ] Questions waiting at the same time are put to the person in rounds of at most five
- [ ] Agreeing with a recommendation is a single action in Dragon and the web UI

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] Several questions waiting at once arrive as one round with a recommended answer each

## Implementation plan

_Populated by `/tasks plan TASK-209` — leave empty until then._
