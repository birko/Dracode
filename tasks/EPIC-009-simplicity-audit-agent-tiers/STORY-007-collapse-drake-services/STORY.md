---
id: STORY-007
parent: EPIC-009
status: planned
created: 2026-05-28
---

# Evaluate collapsing DrakeExecutionService + DrakeMonitoringService + ReasoningMonitorService

## User story

As a KoboldLair maintainer, I want to know which of the three Drake-related periodic services can be merged, so I can reduce moving parts without losing the safety properties each provides.

## Behaviour

- Three services with overlapping responsibilities and cycles:
  - `DrakeExecutionService` (30s) — picks up `Analyzed` projects, creates Drakes, summons Kobolds
  - `DrakeMonitoringService` (60s) — monitors stuck Kobolds, handles timeouts
  - `ReasoningMonitorService` (45s) — detects stuck loops, stalled progress, repeated errors, budget exhaustion, creates escalation alerts
- **Hypothesis 1**: `DrakeMonitoringService` and `ReasoningMonitorService` could collapse into one liveness service that handles BOTH stuck-Kobold detection AND stalled-reasoning detection. Their responsibilities overlap conceptually.
- **Hypothesis 2** (less obvious): `DrakeExecutionService` could become reactive (triggered by `ProjectStatusChangedEvent` on `New → Analyzed` transitions) instead of polled every 30s. Reduces idle CPU + makes the system more responsive.
- **Test plan**:
  - Measure current overhead: how often does each service do real work vs. tick and exit empty-handed? (Add per-service "no-op tick" telemetry, run for 24h.)
  - Identify event-trigger boundaries: which state transitions could replace which polls?
  - Prototype the merger (Hypothesis 1 first — lower risk), run a 24h soak test on a project pipeline
- **Risk**: each service has its own retry/circuit-breaker logic. Merging could create one large failure domain. Mitigate with per-responsibility try/catch inside the merged service.
- **Open question**: do the 30s/60s/45s cycle times matter as currently chosen, or are they arbitrary? Document the rationale before changing them.
