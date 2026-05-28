---
id: TASK-004
parent: EPIC-003
status: todo
priority: P2
assignee: ai
created: 2026-05-28
depends-on: []
blocks: [TASK-005]
pr: null
github-issue: null
jira-key: null
---

# Drag-and-drop tab reordering

## Context

DraCode Web UI tabs are currently fixed-order. Users want to reorder them via drag-and-drop. Pairs with TASK-005 (workspace save/load) which needs an ordered tab list anyway.

## Acceptance criteria

- [ ] HTML5 Drag API or native draggable on tab handles
- [ ] Visual drop-zone indication during drag
- [ ] New order persisted to localStorage (or workspace config if TASK-005 lands first)
- [ ] Keyboard reorder accessibility (arrow keys with modifier)
- [ ] Works in latest Chrome/Edge/Firefox/Safari

## Out of scope

- Cross-window tab moves (single-window only)

## Implementation plan

_Populated by `/tasks plan TASK-004` — leave empty until then._
