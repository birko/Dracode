---
id: TASK-206
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
blocks: [TASK-207]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Dragon answers only from recorded decisions and the specification, otherwise asks the person to define it

## Context

FEATURE-078 D19. Dragon is the last level before the person. It answers only from recorded decisions and the
specification's own text, never from its own judgement; when the specification does not cover something, it asks the
person to define it.

Decisions: FEATURE-078 D19.

## Acceptance criteria

- [ ] Dragon answers a question a recorded decision or the specification's text settles, quoting it
- [ ] A question neither settles reaches the person, asking them to define it
- [ ] Dragon never answers from its own judgement

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] A question the specification does not cover reaches the person through Dragon as a request to define it

## Implementation plan

_Populated by `/tasks plan TASK-206` — leave empty until then._
