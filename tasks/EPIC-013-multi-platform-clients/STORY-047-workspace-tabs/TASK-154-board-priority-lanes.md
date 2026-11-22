---
id: TASK-154
parent: STORY-047
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-153, TASK-183]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Board: priority lanes in To do, drag to reprioritise

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

The To do column is split into priority lanes (Critical, High, Normal, Low). Dragging a card to another lane sets its
priority through the existing call (`POST /api/v1/tasks/{id}/priority`). The server's execution order stays
priority → dependencies → complexity; no server change. Reprioritising is a desktop action (D21).

Decisions: FEATURE-026 D16.

## Acceptance criteria

- [x] To do shows the four lanes; dragging a card to another lane changes its priority and it stays there after a refresh
- [x] A keyboard way to move a card to a lane exists
- [x] A failed priority call puts the card back and shows an error
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Reordering inside a lane (not decided)

## Human test plan

- [x] Drag a Normal task to Critical: it is picked up before the other Normal tasks

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Board:** To do is `b-kanban`'s only unlocked column, with four lanes (Critical, High, Normal, Low; a lane's id is
  the priority in lower case) and `sortable: false`; the other columns stay locked, so a card can neither leave To do nor
  enter it. A `card-move` between lanes posts `POST /api/v1/tasks/{id}/priority`; while the call runs the card keeps its
  new lane even if a refresh still carries the old priority. A refused or failed call puts the card back
  (`revertMove`, or a redraw from the tasks if a refresh came in between) and a line above the board says why. Keyboard:
  focus a card, Space picks it up, ↑/↓ go lane to lane, Enter drops (b-kanban). `maxNestingDepth: 0` (Birko.Web
  v0.4.22) removes the sub-card spacer.
- **Server change, though the task expected none:** the REST priority call set only the store row, which Drake's next
  save overwrote (as the REST retry did before TASK-153), so a dragged card would have kept its old place in Drake's
  order. `DrakeFactory.SetTaskPriorityAsync` sets it in the task's Drake and file, else its file, else the store, and
  updates the store row before returning; a Done task is refused (400). The REST call and Warden's `set_task_priority`
  both use it; the tool's own file fallback (only when the project had no Drake at all) is gone. Numeric priority
  strings are refused.
- **Tests:** .NET `TaskPriorityTests` (Drake, file, store-only, Done/unknown) and a REST test that the task file Drake
  reads gets the priority (red on the old endpoint: the file kept Normal). e2e `board.spec.ts`: the lanes and a drag
  that persists through a refresh, a keyboard move, a refused change put back with its reason, and only To do taking
  moves; the four new ones red on the old board. Suites: .NET 509/509, e2e 73/73.
- **Human test** (throwaway Testing server on a scratch copy, JWT on, LAN model): `waver2` paused with three Normal
  tasks added to one area; dragging `[impl-4]` from Normal to Critical saved Critical to its task file and it stayed
  after a refresh. With the server restarted and the project running (one Kobold at a time), Drake picked `[impl-4]`
  first, then `[impl-2]` and `[impl-3]`. Light and dark at 1400px, no console errors.
