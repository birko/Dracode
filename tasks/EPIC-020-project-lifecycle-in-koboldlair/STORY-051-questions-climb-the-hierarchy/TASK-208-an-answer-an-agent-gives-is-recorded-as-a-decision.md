---
id: TASK-208
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

# An answer an agent gives is recorded as a decision the person can see and overturn

## Context

FEATURE-078 D21. Every answer an agent gives instead of the person is recorded as a decision (who answered, and
why). The person sees it for information (Dragon, Needs you) and can overturn it, which reopens the work it affected.

Decisions: FEATURE-078 D21.

## Acceptance criteria

- [ ] Every agent answer is stored as a decision naming the agent and its reason
- [ ] The person sees agent answers for information in Dragon and the web UI
- [ ] Overturning an agent answer records the person's answer and reopens the work it affected

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [ ] Overturn an agent's answer on a throwaway server: the affected task reopens with the person's answer

## Implementation plan

_Populated by `/tasks plan TASK-208` — leave empty until then._
