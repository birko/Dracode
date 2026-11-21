---
id: TASK-153
parent: STORY-047
feature: FEATURE-026
status: done
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-150]
blocks: [TASK-154, TASK-164]
findings: []
pr: 16
github-issue: null
jira-key: null
---

# Board tab: status kanban with Retry

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

The Board tab shows the project's tasks (`GET /api/v1/projects/{id}/tasks`) in fixed status columns: To do,
In progress, Blocked·Failed, Done. The agents move the cards; the user does not drag between status columns (D16).
No push of task changes reaches a browser today, so the board refreshes while visible (the plan picks polling or
another way). Failed cards have Retry (`POST /api/v1/tasks/{id}/retry`). When Dragon is expanded the board turns
into a compact list (D13).

Decisions: FEATURE-026 D16.

## Acceptance criteria

- [x] Four status columns; each card shows title, agent type, priority and, on a failed card, the start of its error
- [x] Retry on a failed card resets it and the card moves to To do
- [x] The board refreshes on its own while visible
- [x] With Dragon expanded, the board shows as a compact list
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Priority lanes and reprioritising (TASK-154)
- Further statuses (D16: later, if missing)

## Human test plan

- [x] Watch a running local project: cards move between columns as the agents work
- [x] Retry a failed task: it leaves Blocked·Failed and runs again

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Server: the REST Retry did not retry.** `POST /api/v1/tasks/{id}/retry` reset only the task store row, but Drake works
  from its tracker and the area's task file and only copies changes into the store: a live Drake kept the task Failed
  (and saved Failed back), and a restart reloaded Failed from the file — while a board reading the store would show the
  card in To do. Retry now goes through `DrakeFactory.RetryFailedTaskAsync` (`TaskRetryOutcome`): in a Drake of the
  project (`Drake.UpdateTask` → Unassigned, error cleared, the task's Kobold let go, task file saved), else in the task
  file (the store row follows), else in the store alone. Dragon's `retry_failed_task` uses it too (its file path never
  updated the store). Tests: `TaskRetryTests` (live Drake, file only, not failed / not found, store only) and an endpoint
  test that the task file is reset (red on the old code).
- **Board** (`views/board-tab.ts`): the workspace's first tab and the default (FEATURE-026 D11's order: Board, Spec, …);
  project cards open it. Columns by status: Unassigned → To do; NotInitialized and Working → In progress (as the
  homepage counts); Failed, BlockedByFailure, AwaitingHumanDecision → Blocked·Failed (the last two with a note);
  Done → Done. Cards sorted by priority; the title is the first line of the task text; a failed card shows the first 160
  characters of its error and a Retry (`b-button`), which moves it to To do or says on the card why it was refused.
- **Refresh:** polls every 5 s while the page is visible (stops when hidden, refreshes on return), and re-renders only
  when the tasks changed, so focus and hover survive. No push of task changes exists yet.
- **Compact list:** a container query on the board's own width (≤ 44rem), so it applies on a phone and will apply when
  Dragon expands beside the board (TASK-152) with nothing to wire.
- **e2e:** `board.spec.ts`, 7 tests, all red on the old client; `workspace.spec.ts` updated for Board as the default
  tab. Suite 60/60.
- **Human test** (throwaway Testing server on a scratch copy, project paths repointed to it, `waver2`'s original spec
  restored so Wyvern did not re-analyse; JWT on, LAN model): the Board showed `review-1` in To do and `impl-1` (Failed,
  "acceptance check failed after 2 reopen(s): Export wave is not declared…") plus a decision-waiting task in
  Blocked·Failed. Retry moved `impl-1` to To do at once; the board then followed it on its own to In progress (25 s) and
  Done (31 s) — the server log shows Drake summoning a Kobold, which resumed its plan and completed. Light and dark at
  1400px and 375px: no horizontal scroll, no console errors.
- **Seen, not changed (TASK-182):** after `impl-1` completed, its `reflect` tool raised a "progress stalled" escalation for
  it (reflections 100→100→100%) and its plan was revised; the task stayed Done.
