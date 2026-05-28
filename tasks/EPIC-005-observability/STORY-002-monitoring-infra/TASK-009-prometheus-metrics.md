---
id: TASK-009
parent: STORY-002
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

# Prometheus /metrics endpoint

## Context

External operators want to scrape DraCode metrics with Prometheus. Composes with `Birko.Telemetry` once `Birko.Telemetry.Prometheus` lands (cross-references that framework epic).

## Acceptance criteria

- [ ] `/metrics` endpoint in Prometheus text format on KoboldLair.Server
- [ ] Exposed metrics:
  - Active agents count (Dragon, Wyvern, Drake, Kobold) by project
  - Task queue depth + throughput
  - Provider API call rates + error rates
  - Token usage per provider per project
  - Task completion rate (done / failed / blocked)
  - Average task duration
  - Kobold resource utilization (% of limit used)
- [ ] Configuration: scrape endpoint enable/disable, basic-auth optional
- [ ] Grafana dashboard JSON template in `docs/monitoring/`

## Out of scope

- Push-gateway support (scrape model assumed)
- Tracing exporter (separate Birko.Telemetry concern)

## Implementation plan

_Populated by `/tasks plan TASK-009` — leave empty until then._
