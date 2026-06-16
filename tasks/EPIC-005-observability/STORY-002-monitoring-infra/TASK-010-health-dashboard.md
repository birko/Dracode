---
id: TASK-010
parent: STORY-002
status: todo
priority: P2
assignee: ai
created: 2026-05-28
depends-on: [TASK-009]
blocks: []
pr: null
github-issue: null
jira-key: null
feature: FEATURE-004
---

# Health dashboard web UI

## Context

Separate `DraCode.KoboldLair.Dashboard` project — real-time read-only monitoring. Control flows through Dragon chat, dashboard just visualizes.

## Acceptance criteria

- [ ] New project: `DraCode.KoboldLair.Dashboard` (Blazor Server or React + SignalR — pick at start)
- [ ] WebSocket / SSE connection to KoboldLair.Server for live data
- [ ] Live view of active agents (Dragon → Wyvern → Drake → Kobolds hierarchy tree)
- [ ] Project progress bars (tasks done / total)
- [ ] Error log stream with filtering
- [ ] Provider status indicators (healthy / rate-limited / down)
- [ ] Kobold resource gauges per project
- [ ] Task timeline visualization
- [ ] Read-only (no actions on dashboard)

## Out of scope

- Mobile responsiveness as primary concern (desktop-first)
- Alert configuration (Prometheus AlertManager handles that)

## Implementation plan

_Populated by `/tasks plan TASK-010` — leave empty until then._
