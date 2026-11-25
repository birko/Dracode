---
id: TASK-223
parent: STORY-053
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

# Repeated failures and monitor alerts become tracked fix tasks, worked by impact

## Context

FEATURE-078 D14. Repeated failures and monitor alerts are retried today; they are filed as tracked fix tasks and
worked through in order of impact.

Decisions: FEATURE-078 D14.

## Acceptance criteria

- [ ] A failure that repeats, or a monitor alert, files a fix task naming what failed
- [ ] Fix tasks are picked by impact before other work of the same priority
- [ ] The same failure does not file a second open fix task

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] A task that fails repeatedly on a throwaway server leaves one fix task behind

## Implementation plan

_Populated by `/tasks plan TASK-223` — leave empty until then._
