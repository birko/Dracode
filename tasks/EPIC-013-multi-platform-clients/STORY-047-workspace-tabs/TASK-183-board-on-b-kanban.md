---
id: TASK-183
parent: STORY-047
feature: FEATURE-026
status: todo
blocked: waiting for Birko b-kanban per-column drag rules, lanes inside a column and a narrow layout (requested 2026-11-21)
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

- [ ] The Board renders through `b-kanban` (on the Birko version that has the three additions), with the same columns,
      card content, Retry, refresh and compact list as TASK-153's; nothing can be dragged between status columns
- [ ] `board.spec.ts` stays green (its selectors adjusted to b-kanban's markup), and the suite stays green

## Out of scope

- The priority lanes themselves (TASK-154, built on this)

## Human test plan

- [ ] On a throwaway server: the Board looks and behaves as before — cards move as agents work, Retry works, compact at
      phone width; no card can be dragged between status columns

## Implementation plan

_Populated by `/tasks plan TASK-183` — leave empty until then._
