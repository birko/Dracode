---
id: TASK-160
parent: STORY-048
feature: FEATURE-026
status: todo
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-151, TASK-152, TASK-024]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Decision cards in Dragon

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Anything an agent layer cannot resolve comes to the user as an answerable decision in Dragon (D15). The server side
is EPIC-014: answerable notifications (`requiresResponse`) and Dragon's prompt round-trip. This task renders them: a
card in the Dragon dock with a badge. A new card does not expand Dragon; the user's click does (D14). Answering sends
the reply back and the card resolves. The same decision appears in the bell and under Needs you (kind `decision`).

Decisions: FEATURE-026 D14, FEATURE-026 D15, FEATURE-026 D17.

TASK-024's render criterion moved here (FEATURE-030 D3, changed 2026-11-17): the card answers through the `/dragon`
message `answer_decision` that TASK-024 adds, and open decisions arrive as `decision_required` cards on every connect
until answered or timed out. The socket handles one message at a time, so an answer sent while Dragon's welcome is still
being written gets its reply after the welcome (seconds with a model that answers); the card should show it as
pending until `decision_answer_result` arrives (found at TASK-024's close).

## Acceptance criteria

- [ ] An incoming decision shows as a card in the shrunk dock with a badge; Dragon does not expand by itself
- [ ] Clicking the card expands Dragon; the card offers the decision's options and free text; answering resolves it
- [ ] The decision also shows in the bell and under Needs you, and leaves both once answered
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The server round-trip (EPIC-014)

## Human test plan

- [ ] Trigger a decision in a local run while on the board: the card and badge appear without the board moving; answer it and the run continues

## Implementation plan

_Populated by `/tasks plan TASK-160` — leave empty until then._
