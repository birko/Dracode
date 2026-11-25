---
id: TASK-204
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-170]
blocks: [TASK-205]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Wyvern becomes the project's overseer: several turns, read-only tools

## Context

FEATURE-078 D25. Wyvern's analysis is one model call with `maxIterations: 1` that must return JSON. As the
project's overseer it works over several turns with read-only access to the specification, its analysis, the decision
ledger, the task lists and the workspace, and it is called both to analyze and to answer questions at its level.

Decisions: FEATURE-078 D25.

## Acceptance criteria

- [ ] Wyvern runs over several turns with read-only tools for the specification, its analysis, the decision ledger, the task lists and the workspace; it has no write or command tools
- [ ] The analysis still ends in the same analysis output, and its existing tests pass
- [ ] Wyvern can be called on a project to answer a question, separately from an analysis

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] Analyze a real project on a throwaway server: the analysis still produces tasks, and the log shows Wyvern looking things up

## Implementation plan

_Populated by `/tasks plan TASK-204` — leave empty until then._
