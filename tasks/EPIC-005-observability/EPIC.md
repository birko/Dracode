---
id: EPIC-005
status: planned
created: 2026-05-28
owner: ai
---

# Observability — Metrics + Health Dashboard

## Area of concern

External operators need a metrics endpoint + a real-time monitoring UI to see what KoboldLair is doing in production.

## Success criteria

- Prometheus `/metrics` exposes agent counts, queue depths, provider rates, token usage, completion stats
- Health dashboard web UI shows live agent hierarchy + task progress + provider status
- Grafana dashboard JSON template ships in docs

## Features

| Feature | Covers | Status |
|---------|--------|--------|
| [FEATURE-004](../../docs/features/FEATURE-004-monitoring-infra/idea.md) | STORY-002 (TASK-009, TASK-010) | idea |
