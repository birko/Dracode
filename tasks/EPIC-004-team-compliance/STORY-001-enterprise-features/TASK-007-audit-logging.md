---
id: TASK-007
parent: STORY-001
status: todo
priority: P1
assignee: ai
created: 2026-05-28
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Audit logging

## Context

Comprehensive audit trail of all user + agent actions for compliance + post-mortem analysis. Pairs with the existing `Birko.Data.EventSourcing` integration for specifications — extends to all relevant actions.

## Acceptance criteria

- [ ] `AuditLog` table with: timestamp, actorId, actorType (user/agent), action, resourceType, resourceId, beforeState?, afterState?, ipAddress, userAgent
- [ ] Recorded on: all Dragon tool calls, all Kobold tool executions, all CRUD on Specifications/Features/Projects, all auth events
- [ ] Append-only — no UPDATE / DELETE permissions on the table
- [ ] Compliance report export (CSV / JSON, filtered by date range + actor + resource)
- [ ] Retention policy configurable (default: keep forever)
- [ ] Dragon tool `view_audit_log` for ad-hoc queries (read-only)

## Out of scope

- Real-time SIEM integration (could be a follow-up Telemetry exporter)
- Anomaly detection on the log (separate analytics concern)

## Implementation plan

_Populated by `/tasks plan TASK-007` — leave empty until then._
