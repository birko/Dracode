---
id: TASK-124
parent: null
feature: null
status: todo
priority: P3
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# ServiceDefaults does not export Birko's traces and metrics

## Context

`DraCode.ServiceDefaults/Extensions.cs` configures OpenTelemetry from the Aspire template but never adds Birko's
`ActivitySource` / `Meter` (`BirkoTelemetryConventions`) or calls `AddBirkoOpenTelemetry`, so store, AI and job
telemetry the framework emits never reaches the Aspire dashboard.

Found in the 2026-10-08 KoboldLair review.

## Acceptance criteria

- [ ] Birko's sources and meters are registered in ServiceDefaults
- [ ] A store call and an LLM call show up as spans in the Aspire dashboard

## Human test plan

Run AppHost, trigger a Kobold run, find Birko spans in the dashboard.

## Implementation plan
