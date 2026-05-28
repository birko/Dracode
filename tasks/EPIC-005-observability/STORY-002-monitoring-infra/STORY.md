---
id: STORY-002
parent: EPIC-005
status: planned
created: 2026-05-28
---

# Monitoring infrastructure

## User story

As an operator running KoboldLair in production, I want a Prometheus scrape endpoint and a real-time monitoring UI so I can see what the system is doing without tailing logs.

## Behaviour

- `/metrics` endpoint in Prometheus text format
- Health dashboard UI as a separate `DraCode.KoboldLair.Dashboard` project
- Live view: active agents, task queue depth, provider status, project progress
- Read-only — control still goes through Dragon chat
