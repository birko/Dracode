---
id: TASK-204
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
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
pr: 79
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

- [x] Wyvern runs over several turns with read-only tools for the specification, its analysis, the decision ledger, the task lists and the workspace; it has no write or command tools
- [x] The analysis still ends in the same analysis output, and its existing tests pass
- [x] Wyvern can be called on a project to answer a question, separately from an analysis

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] Analyze a real project on a throwaway server: the analysis still produces tasks, and the log shows Wyvern looking things up

## Implementation plan

Planned at pick (2026-11-26):

1. Red first (`WyvernOverseerTests`): Wyvern's tools are exactly `list_files`, `read_file`, `search_code` and
   `project_decisions`; an analysis that reads `specification.md` in one turn and answers in the next still yields its
   tasks; `ConsultAsync` answers after using `project_decisions`. The tests did not compile before the change.
2. `WyvernAgent`: read-only `CreateTools` (`ProjectDecisionsTool` when it has the project's decisions, also settable
   with `UseDecisions`), the analysis run with up to `OverseerTurns` (8) turns and told it may look things up, and
   `ConsultAsync`.
3. `WyvernFactory`: the agent works from the project folder, not only `workspace/`; `ProjectDecisions` (set by the
   server from the decision store, `GetForProjectAsync`) feeds the tool.

## Notes

- Task refinement (`RefineTaskAsync`) still runs one turn; it is not part of this task.
- Full .NET suite 698 passed; no client change.
- **Human test** (throwaway server, vLLM `lan2`):
  - `todo204`, a complete specification: analyzed into 7 tasks in 4 areas with one model call — nothing to look up.
  - `todo204b`, whose specification says the commands are in `commands.md` in the project folder (a file holding four
    commands, `purge` among them): Wyvern took 3 model calls, and the analysis plans `todo purge`, which exists only in
    that file, so it read it. Drake took the project up.
