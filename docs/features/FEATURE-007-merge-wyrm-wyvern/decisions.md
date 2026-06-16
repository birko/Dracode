---
id: FEATURE-007
created: 2026-05-31
---

# Evaluate merging Wyrm + Wyvern into one analyzer — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run an A/B comparison of the split (two-agent) pipeline vs. a single merged analyzer on 5-10 representative specs | proposed | The split is conceptual, not technical; needs measured evidence before any merge | — | — | — |
| D2 | Score both pipelines on requirement coverage, constraint propagation, total AI cost per spec, time-to-ready, and downstream worker escalation rate | proposed | These are the dimensions that determine whether the second analysis run earns its cost | — | — | — |
| D3 | Specifically test that a merged single-call analyzer still anchors task descriptions back to project constraints | proposed | Today the pre-analysis constraints feed the detailed breakdown; merging risks losing that anchoring | — | — | — |
| D4 | Decide whether a merged analyzer keeps the pre-analysis recommendation as a separate artifact or folds it into the analysis output | proposed | Depends on whether any consumer other than the detailed analyzer actually reads it | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-005 behaviour and open questions.
