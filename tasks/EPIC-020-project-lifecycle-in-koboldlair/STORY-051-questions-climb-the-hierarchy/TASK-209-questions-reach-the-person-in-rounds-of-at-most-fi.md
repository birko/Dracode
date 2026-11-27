---
id: TASK-209
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
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
pr: 85
github-issue: null
jira-key: null
---

# Questions reach the person in rounds of at most five, each with a recommended answer

## Context

FEATURE-078 D22. Questions that reach the person come in rounds of at most five, each with a recommended answer and
its one-line reason, so agreeing is one word.

Decisions: FEATURE-078 D22.

## Acceptance criteria

- [x] Every question that reaches the person carries a recommended answer and its reason
- [x] Questions waiting at the same time are put to the person in rounds of at most five
- [x] Agreeing with a recommendation is a single action in Dragon and the web UI

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] Several questions waiting at once arrive as one round with a recommended answer each

## Implementation plan

Decided with the owner while planning (FEATURE-078 D22, now `changed` with the detail): the asking agent gives the
recommendation; a round is a project's first five open questions, the next coming in as one is answered; one click
accepts a recommendation, one more the whole round, and Warden can accept them on the person's word.

1. `AskHumanQuestion` gains `Recommended` and `RecommendationReason` (optional, so older questions still read).
   `AskHumanTool.Problem` with a recommendation refuses a question without one, without a reason, or with one the
   question does not accept, and stores it in its answer form; `ask_human` requires `recommended` and
   `recommended_because`; Wyvern's questions carry `recommended`/`recommendedBecause` (its prompt asks for them) and one
   without is dropped like any failing question.
2. The card carries `recommended` and `recommendedBecause` (`PendingDecisionNotification.Recommendation`); an agent's
   answer offered for review (TASK-208) recommends Keep for its quote.
3. `DecisionDesk.AcceptRecommendedAsync` answers one or every open decision with its recommendation; Warden's
   `pending_decisions` lists the recommendation and gains `accept_recommended`. Warden's list now shows an agent's answer
   offered for review with its Keep-first options (a TASK-208 gap).
4. The web dock: the recommendation with Accept on every card, closed ones included; at most five cards (`ROUND_SIZE`)
   with "N more waiting"; "Accept all recommended (n)" over a round with two or more; "Agent answered" on a review card.

## Notes

- Red first only as compile failures for the new API; 22 existing tests broke as intended (their questions carried no
  recommendation) and their fixtures now give one.
- Escalations, approvals and questions asked before this carry no recommendation; the round's "Accept all" skips them.
- A round is drawn by the client: the server still sends every open card, and the header count shows them all.
- Full .NET suite 726 passed; e2e 191 passed (3 new dock tests).
- Human test on the throwaway `projects193`: a first specification with open choices (quotes209) was planned by Wyvern
  without asking. A second, menu209, said the owner must choose three things; Dragon first asked for the picks itself and,
  told to leave them open, approved it. Wyvern then asked 3 questions in one round, each with a recommendation and a
  one-line reason (EUR — "the most conventional format"; hard-coded in menu.py; plain text). One chat message, "I agree
  with all the recommended answers", had Warden answer all three with their recommendations; the re-analysis planned
  "menu.py with hard-coded EUR menu and plain-text output". Not confirmed: whether Warden used `accept_recommended` or
  three `answer` calls (council tool inputs are not logged). The web dock's round and buttons were seen only in e2e.
- Seen on the way: Dragon told the person the three answers "now live as decision records, not in the spec text",
  while the log shows Dragon's level wrote each into the specification (TASK-207); its summary was wrong, not the data.
