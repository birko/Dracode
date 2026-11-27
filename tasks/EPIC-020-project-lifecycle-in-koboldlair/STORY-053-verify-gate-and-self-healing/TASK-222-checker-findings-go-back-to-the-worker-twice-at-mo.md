---
id: TASK-222
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-217]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Checker findings go back to the worker, twice at most, then escalate

## Context

FEATURE-078 D9, D29. A task whose checks fail goes back to its worker with the findings; after two such reopens
the third failure escalates, the same limit the acceptance check uses today.

Decisions: FEATURE-078 D9, D29.

## Acceptance criteria

- [x] Findings reopen the task with the findings in the worker's context
- [x] The third failure escalates instead of reopening
- [x] One limit covers the acceptance check and the checkers

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] A task failing a check three times escalates with its findings

## Implementation plan

Already in place from TASK-217: a checker's findings reopen the task like an unmet promise, appended to its description
(the worker's context) and sent through the WrongApproach plan revision, under the same `AcceptanceAttempts` /
`MaxAcceptanceReopens` (2) as the acceptance check's own checks. What was left was what "escalates" means.

Owner's decision (2026-11-27): the third failure goes to a person's card, not straight to fail + fix task.

1. `Drake.ReopenOrFailAsync` → `ReopenOrEscalateAsync(task, unmet, kobold, worktreePath, byPerson)`: at the limit,
   `EscalateAcceptanceAsync` commits the work and parks the task on an `Approval` decision carrying an `EscalationAlert`
   (WrongApproach, summary = this time's findings), which marks it as an escalation (`AcceptanceReview.IsEscalation`).
   No timeout. Without a decision store, or if the card cannot be raised, it fails with the fix task as before.
2. `AcceptanceReview.EscalationQuestion`: the task, "failed its checks N times", each earlier attempt's reopen note
   (as "attempt 1: …"), this time's findings, and the three answers.
3. `PendingDecisionNotification.ResponseSchemaFor`: an escalation offers Approve and "Handle it automatically", plus free
   text.
4. `ApplyReviewAsync`: "Handle it automatically" on an escalation fails the task with a permanent error and files the
   fix task from the stored summary; Approve marks it Done; anything else is a note that reopens the task with
   `byPerson: true`, which passes the limit. Its next failure escalates again.

## Notes

- Red first: 5 new tests in `TaskAcceptanceGateTests` (escalation card with every attempt's findings; Handle it
  automatically → Failed + Critical fix task; a person's note reopens past the limit and the next failure escalates
  again; Approve → Done; no decision store → fails as before) and 1 in `TaskCheckerTests` (a checker's finding three
  times → card). The old "at the cap it fails" test was replaced. Full suite: 747 passed.
- Human test (2026-11-27), a scratch harness with a real Drake, SQLite decisions and the real intent checker on the live
  model (vLLM `finstat`): a `menu.py` missing its required last line finished three times. Finishes 1 and 2 reopened it
  (attempts 1, 2), finish 3 parked it on a card with no timeout: "failed its checks 3 times and is no longer sent back
  on its own", both earlier attempts' `[intent] missing` findings quoting the requirement, and this time's. Answers
  offered: Approve, Handle it automatically, free text. "Handle it automatically" → Failed ("acceptance check failed
  after 2 reopen(s): …") and one fix task naming the finding. The first run showed "attempt 1): …"; fixed to
  "attempt 1: …".
- Not seen live: the card in the web dock / Needs you (it is an ordinary `Approval` card with a different option list).
- A person's rejection of an ordinary review card now also reopens past the limit (it used to fail the task at the
  cap): a person in the loop decides, not the counter.
