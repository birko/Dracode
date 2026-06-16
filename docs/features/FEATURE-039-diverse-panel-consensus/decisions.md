---
id: FEATURE-039
created: 2026-05-31
---

# Diverse-panel consensus (heterogeneous agents) — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Build the panel on the shared consensus mechanism, using distinct specialist agent types as panellists rather than N runs of one agent | proposed | — | — | — | — |
| D2 | Each panellist returns a structured verdict (approve/reject + score + reason + concerns); the agreement rule aggregates across the diverse perspectives | proposed | — | — | — | — |
| D3 | Choose the evaluation target during planning from: final Kobold module output, escalation-resolution correctness, or Wyvern breakdown soundness | proposed | — | — | — | — |
| D4 | Gate behind a feature flag and a priority threshold (e.g. only critical tasks), with the cost multiplier documented | proposed | — | — | — | — |
| D5 | Persist each panel review as a consensus-verdict reasoning record capturing every panellist's individual view, not just the aggregate | proposed | — | — | — | — |
| D6 | Bound the number of panel rounds to avoid deadlock/thrash, then fall through to a human gate or accept-with-warning per policy | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-036 behaviour bullets.
