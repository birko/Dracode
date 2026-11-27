---
id: TASK-217
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: [TASK-218, TASK-219, TASK-220, TASK-221, TASK-222]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: 86
github-issue: null
jira-key: null
---

# The acceptance check runs checker agents on every task, cheapest first

## Context

FEATURE-078 D8 (changed), D10, D30. The acceptance check (TASK-025) already holds a finished task to its own
description before Done. It becomes the verify gate: purpose-built checker agents run on every task, cheapest first,
after its own checks. This task builds the runner and the checker contract; the checkers are their own tasks.

Decisions: FEATURE-078 D8, D10, D30.

## Acceptance criteria

- [x] The acceptance check runs a list of checker agents after its own checks, cheapest first, on every task
- [x] Each checker returns findings in one shape; none passes the task to Done while a finding is open
- [x] With no checker registered, behaviour is today's

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

N/A — the runner is asserted by tests; each checker carries its own human test

## Implementation plan

1. `Services/Checks/TaskChecks.cs`: the contract — `ITaskChecker` (`Name`, `Cost`, `CheckAsync`), one finding shape
   (`TaskFinding`: checker, kind, text, evidence; one line as `ToString`), the input (`TaskCheckInput`: the task, its
   workspace, its project) — and `TaskCheckRunner`: cheapest first (then by name), stops at the first checker with
   findings, a checker that throws is reported as unable to run and the rest still run.
2. Drake's acceptance check runs the runner after its own checks when those found no broken promise; findings become
   unmet promises (reopen through the same route, the same `AcceptanceAttempts` and cap), a checker that could not run
   goes to the review card. `DrakeFactory.TaskChecks` hands each Drake its runner; empty by default.
3. `Agents/CheckerAgent.cs` (D10): the base of a purpose-built checker — a tool-less `OrchestratorAgent` and an
   `ITaskChecker`; a checker gives its role, process, rules and material, the base keeps the prompt conventions (role
   line, depth guidance, numbered process, rules in order, schema in the message) and reads `{ "findings": [...] }`; a
   reply without that list throws, so it can never count as a pass.

## Notes

- Red checked by removing the runner call from Drake: the three Drake tests failed, the runner and agent tests (which
  need no Drake) did not depend on it.
- "Every check runs on every task" (D30) is read as no task skipping a check; within one finish the run stops at the
  first checker with findings, since the task is reopened and every checker runs again when it next finishes.
- The checkers run only when the task's own promises held (they are the cheapest check), and on a task whose own
  checks sent something to a person, so the review card lists both.
- No checker is registered on the server yet; the first is TASK-218. Full .NET suite 736 passed.
- STORY-051 closed in this branch (every task done).
