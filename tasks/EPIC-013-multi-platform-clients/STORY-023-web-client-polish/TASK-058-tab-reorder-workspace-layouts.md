---
id: TASK-058
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-034]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Drag-and-drop tabs + workspace layout save/load

## Context

The polish items reclaimed from the deleted EPIC-003. Drag-and-drop tab reordering in the project workspace view, and named workspace layouts (open panels, splitter sizes, active tabs) persisted to the user profile on the server. Builds on the existing Shadow-DOM client; persistence coordinates with STORY-017's `users` table (TASK-034) — likely a `user_workspaces` table or `user_settings` extension.

## Acceptance criteria

- [ ] Drag-and-drop reordering of tabs in the workspace view; order persists across reloads
- [ ] Named workspace layouts: save current layout (panels/splitters/active tabs), load by name, delete
- [ ] Layouts persisted server-side keyed to the authenticated user (coordinate schema with TASK-034)
- [ ] Tests where supported

## Out of scope

- The `users` table itself (TASK-034)
- Other polish items (TASK-055/056/057)

## Human test plan

- [ ] Reorder tabs by dragging → reload → order preserved; save a layout "wide", change it, load "wide" → original layout restored; confirm it persists across a logout/login (server-side, per user)

## Implementation plan

_Populated by `/tasks plan TASK-058` — leave empty until then._
