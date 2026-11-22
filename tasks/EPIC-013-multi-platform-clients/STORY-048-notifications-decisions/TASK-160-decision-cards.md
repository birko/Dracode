---
id: TASK-160
parent: STORY-048
feature: FEATURE-026
status: done
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-151, TASK-152, TASK-024]
blocks: []
findings: []
pr: 22
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

- [x] An incoming decision shows as a card in the shrunk dock with a badge; Dragon does not expand by itself
- [x] Clicking the card expands Dragon; the card offers the decision's options and free text; answering resolves it
- [x] The decision also shows in the bell and under Needs you, and leaves both once answered — the bell part; Needs
      you does not exist yet: TASK-147, which builds it, now lists open decisions as items (moved there from its
      out-of-scope list)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The server round-trip (EPIC-014)

## Human test plan

- [x] Trigger a decision in a local run while on the board: the card and badge appear without the board moving; answer it and the run continues

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Getting the cards:** the web dock never sent `client_ready`, so it got no open decisions. `dragon-connection.ts`
  now sends it after each `project_selected` and resumed session (clearing its cards first, so ones answered or timed
  out while away go). Cards are kept for every project the user can see; the dock shows the followed project's
  (`project_selected` carries the name, as the cards do). A live `decision_required` adds or replaces one,
  `decision_resolved` retires it, a replayed notification is ignored (client_ready brings what is still open).
  `answer(id, text)` sends `answer_decision`; the card is pending until `decision_answer_result`: `InvalidAnswer`
  keeps it open with the server's reason, any other result retires it and puts the server's message in the chat.
- **Dock** (`dragon-dock.ts`): the cards sit above the chat (up to 45% of its height, scrolling), the count as a badge
  in the header. A card shows its kind (Question, Escalation, Review), deadline and question; clicking it opens it —
  focus enters the dock, so Dragon widens (TASK-152) — with the context, an option button each and, when allowed, a
  free-text box (Enter or Send). A new card never takes focus, so Dragon never widens by itself. Cards are drawn
  outside the morph (`data-morph="skip"`), only when they change, with drafts kept and focus put back in the same card
  (or the chat input once it is answered), so a redraw does not narrow Dragon. Every value is escaped.
- **Bell:** the server already marked a decision's `decision_required` read when it resolved, but then added an unread
  "Decision answered: …", so the bell's count did not drop and the person was told what they had just done.
  `DecisionNotifications` (moved out of `Program.cs`) now keeps an answered decision's `decision_resolved` read; a
  timeout stays unread, since nobody answered. It is still pushed live, so other sessions retire the card. The dock
  refreshes the bell when a card goes.
- **Found by the human test — `client_ready` woke Dragon:** a `client_ready` arriving after the welcome fell through
  to the chat handler and was queued as an empty user message, saved to the conversation, and set Dragon (a model
  run) going on every project opened. It now returns after the welcome check.
- **Tests:** e2e `dragon-decisions.spec.ts`, 7 tests, all red on the old client (cards and count without widening, a
  live card not moving the page, opening, options, pending, resolved with the bell dropping it, free text with Enter,
  an invalid answer, a question without free text, a card retired from elsewhere, escaping). .NET
  `DecisionNotificationsTests` (answered leaves nothing unread but is pushed; a timeout says so unread; red without the
  change) and `DragonProjectSocketTests.Client_ready_after_selecting_a_project…` (red: Dragon started typing). Suites:
  .NET 512/512, e2e 86/86.
- **Human test** (throwaway Testing server on a scratch copy, JWT on, LAN model; `waver2`'s Board, where a Kobold's
  real `ask_human` question waits): the card and "1 decision" showed in the 360px dock; clicking it widened Dragon to
  815px with the board not moving; the question's three options, context and free text were there; choosing "Keep the
  spec…" resolved it, the chat said so, the bell went 7 → 6, and the task went from Waiting for your decision to In
  progress and Done. The server log showed no Dragon run. A decision raised live while watching was not reproduced on
  the real stack (it needs an agent to ask one); the e2e spec covers it.
