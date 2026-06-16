---
id: FEATURE-050
created: 2026-02-04
---

# Sectioned project configuration — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Adopt a sectioned project-configs.json format split into project, agents, security, and metadata sections | approved | shipped in 2.4.2 | 2026-02-04 | team | — |
| D2 | Give each agent type its own settings (enabled, provider, model, maxParallel, timeout) with a separate Kobold Planner block | approved | shipped in 2.4.2 | 2026-02-04 | team | — |
| D3 | Add a sandbox mode (workspace / relaxed / strict) to control per-project access | approved | shipped in 2.4.2 | 2026-02-04 | team | — |
| D4 | Record a createdAt audit timestamp on every project | approved | shipped in 2.4.2 | 2026-02-04 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-04 — feature created; decisions seeded from the shipped 2.4.2 sectioned configuration work.
