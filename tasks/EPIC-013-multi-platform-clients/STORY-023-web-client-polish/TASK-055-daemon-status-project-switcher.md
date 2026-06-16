---
id: TASK-055
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Web header: daemon status indicator + project switcher

## Context

Header affordances in `DraCode.KoboldLair.Client`. The client is **already a modern TypeScript/Shadow-DOM codebase** (`app-shell.ts`, `views/`, `components/`, existing `server-selector.ts`) — these build on that foundation, not the archived vanilla UI. Daemon status: green/red dot + click for daemon info. Project switcher: fast multi-session-aware switch that preserves chat scroll position.

## Acceptance criteria

- [ ] Daemon status indicator component in the header: connected (green) / down (red), click → daemon info popover
- [ ] Project switcher in the header switches active project without losing per-session chat scroll position
- [ ] Both implemented as Shadow DOM components consistent with existing `components/`
- [ ] Multi-session awareness: switching projects respects the existing session model
- [ ] Tests where the component framework supports them

## Out of scope

- OAuth login / key management (TASK-056)
- Run viewer (TASK-057), tab/layout (TASK-058)

## Human test plan

- [ ] Stop the daemon → indicator turns red; restart → green; click → info popover shows port/version
- [ ] Open chat in project A (scroll up), switch to B and back → A's scroll position is preserved

## Implementation plan

_Populated by `/tasks plan TASK-055` — leave empty until then._
