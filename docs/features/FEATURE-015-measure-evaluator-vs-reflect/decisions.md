---
id: FEATURE-015
created: 2026-05-31
---

# Measure: does external evaluator catch failures the reflect tool misses? — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Replay 50-100 historical tasks spanning successes, silent failures, escalations, and human-flagged-wrong jobs | proposed | A representative mix is needed to judge the reviewer fairly | — | — | — |
| D2 | Re-run each task's actions and capture the reviewer's verdict after each one | proposed | Reproduces real conditions to score the reviewer against known outcomes | — | — | — |
| D3 | Measure correct catches versus false alarms, and compare against the worker's existing self-check | proposed | Tells us whether the reviewer adds coverage or just duplicates self-reflection | — | — | — |
| D4 | Apply fixed thresholds: strong catch + few false alarms → enable for top-priority tasks; too many false alarms → drop; in between → refine the rubric | proposed | Turns the measurement into an evidence-based, pre-agreed decision | — | — | — |
| D5 | Deliver a decision memo plus the saved replay dataset for future re-evaluation | proposed | Preserves the evidence so the call can be revisited as the design evolves | — | — | — |

## History log
- 2026-05-31 — feature created from STORY-013; decisions seeded from the story's data set, replay setup, metrics, and decision criteria, all proposed pending /feature decide. Depends on FEATURE-013 and FEATURE-014 shipping first.
