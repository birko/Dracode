---
id: TASK-183
parent: STORY-047
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-21
depends-on: [TASK-153]
blocks: [TASK-154]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Board on Birko's b-kanban

## Context

TASK-153 built the Board's status columns by hand (`src/views/board-tab.ts`) without checking for Birko's `b-kanban`,
though FEATURE-026 D9 asks for Birko components over local copies. `b-kanban` has cards in columns, drag and keyboard
moves between columns (`card-move`) and custom card rendering, but three things the Board needs are missing, so on
2026-11-21 they were requested from the Birko session:

- **Per-column drag rules:** the status columns are moved only by the agents (D16), while TASK-154's priority lanes in To
  do take drags and keyboard moves; `disabled` is all or nothing today. A refused or failed move must be able to roll back.
- **Lanes inside a column:** To do holds stacked priority lanes (TASK-154).
- **A narrow layout:** columns have a 16rem minimum and scroll sideways; the Board turns into a compact list in narrow
  space (a phone, or Dragon expanded, D13/D22) with no horizontal scroll.

## Acceptance criteria

- [x] The Board renders through `b-kanban` (on the Birko version that has the three additions), with the same columns,
      card content, Retry, refresh and compact list as TASK-153's; nothing can be dragged between status columns
- [x] `board.spec.ts` stays green (its selectors adjusted to b-kanban's markup), and the suite stays green

## Out of scope

- The priority lanes themselves (TASK-154, built on this)

## Human test plan

- [x] On a throwaway server: the Board looks and behaves as before — cards move as agents work, Retry works, compact at
      phone width; no card can be dragged between status columns

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Birko.Web v0.4.21** (FEATURE-022) added what the Board needed. `board-tab.ts` now renders one `b-kanban`: the four
  status columns, each `locked` (no person moves a card into or out of it, by mouse or keyboard; the board says why)
  and `sortable: false` (cards keep the priority order the view gives them as `order`). `stackBelow: '44rem'` replaces
  the container query: below that board width the columns stack one per row, and the cards render compact (one-line
  titles), read from the board's `stacked` attribute when the cards render.
- **Cards** come from `renderCard`, inside b-kanban's shadow root, so they are styled inline. A failed card's red edge is
  the card's `color`. Retry is a `b-button` on the card; b-kanban treats it as the card's own control (no drag, no
  card click), and the view handles its click by delegation on the board's shadow root.
- **Refreshes hand over cards, not a re-render:** while the board is on screen the view calls `setCards`. Re-rendering
  the view would morph the `b-kanban` element and remove the `stacked` attribute it sets on itself, unstacking a narrow
  board (reported to Birko, with `maxNestingDepth: 0` not turning off the sub-card spacer, which leaves an empty strip
  at each card's left).
- **Tests:** `board.spec.ts` on b-kanban's markup, plus a test that every column is locked and a drag leaves the card
  put, and a check that a refresh keeps a narrow board stacked (red when the view re-renders). e2e 70/70.
- **Human test** (throwaway Testing server on a scratch copy, JWT on, LAN model; `waver2`): a mouse drag to Done and a
  keyboard move were both refused ("To do is locked: its cards cannot be moved."). Retry moved `impl-1` to To do, then
  live to In progress and Done as Drake ran it. Not stacked at 1400px; stacked at 900px (Dragon docked) and 375px in
  light and dark, no horizontal scroll, no console errors. Every column header now shows b-kanban's "Locked" chip and
  each column a "N cards" footer; a way to tone both down was asked of Birko.
