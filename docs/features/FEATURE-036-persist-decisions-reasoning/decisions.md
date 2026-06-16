---
id: FEATURE-036
created: 2026-05-31
---

# Persist structured decision & reasoning records — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add a structured database record capturing project, task, agent, timestamp, kind (decision / reflection / escalation / consensus-verdict), reasoning payload, confidence, and links to the artifact it concerns | proposed | — | — | — | — |
| D2 | The self-assessment (reflect) output is persisted as a record instead of being ephemeral, and escalation decisions plus their routing are recorded | proposed | — | — | — | — |
| D3 | Records are queryable per project / task / agent and feed cross-project learning | proposed | — | — | — | — |
| D4 | Records are designed as the substrate later consensus / voting / evaluator features read from and write verdicts back into | proposed | — | — | — | — |
| D5 | High-volume reasoning is written async/batched off the correctness-critical path so it doesn't add latency | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-033 behaviour bullets under EPIC-016.
