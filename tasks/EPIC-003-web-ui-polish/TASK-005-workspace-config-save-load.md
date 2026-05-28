---
id: TASK-005
parent: EPIC-003
status: todo
priority: P2
assignee: ai
created: 2026-05-28
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Save/load workspace configurations

## Context

Users want to save a named workspace (which tabs are open, their order, sizes, active panel) and restore it later. Multiple workspaces per user.

## Acceptance criteria

- [ ] `WorkspaceConfig` model: name, openTabs[], tabOrder[], paneSizes, activeTab
- [ ] Save / load endpoints on KoboldLair.Server (`/api/workspaces/*`)
- [ ] Per-user workspace storage (persists across logins)
- [ ] UI: workspace picker dropdown + Save / Save As / Delete actions
- [ ] Default workspace auto-loads on login
- [ ] Import / export workspace as JSON for sharing
- [ ] Unit + integration tests

## Out of scope

- Team-shared workspaces (separate concern; pairs with EPIC-004)

## Implementation plan

_Populated by `/tasks plan TASK-005` — leave empty until then._
