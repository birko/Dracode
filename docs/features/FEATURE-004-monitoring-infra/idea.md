---
id: FEATURE-004
created: 2026-05-31
owner: human
status: idea
---

# Monitoring & observability infrastructure

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When KoboldLair runs in production, operators have no good way to see what it's doing. Today the only window into the system is reading raw logs, which doesn't scale and hides the big picture — how many agents are active, how deep the work queue is, whether a provider is healthy, and how far each project has progressed.

## Proposed shape

Add two complementary views. First, a standard metrics endpoint that monitoring tools (Prometheus) can scrape automatically — exposing agent counts, queue depth, provider call rates and errors, token usage, and completion stats — plus a ready-made dashboard template. Second, a dedicated, real-time monitoring web app that shows the live agent hierarchy, per-project progress bars, an error stream, and provider health at a glance. The monitoring views are read-only; all control still happens through the Dragon chat.

## Out of scope (initial)

- Push-gateway support for metrics (scrape model assumed)
- Tracing exporter (separate telemetry concern)
- Mobile-first dashboard layout (desktop-first)
- Alert configuration (handled by Prometheus AlertManager, not the dashboard)

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
