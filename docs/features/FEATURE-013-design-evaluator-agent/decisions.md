---
id: FEATURE-013
created: 2026-05-31
---

# Design EvaluatorAgent — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add a separate reviewer agent, independent of the worker's own self-reflection | proposed | An independent critic catches failures the worker's self-check misses | — | — | — |
| D2 | Reviewer returns a structured verdict: approve/reject, score, reason, missing items, optional correction | proposed | A consistent, machine-readable verdict is needed to drive the worker's next step | — | — | — |
| D3 | Score work against the step's agreed expectations using a fixed rubric (progress, regressions, did the saved change really contain the expected content) | proposed | A stable rubric keeps judgments consistent and explainable | — | — | — |
| D4 | Reviewer is read-only — it never executes tools or makes changes | proposed | Keeps the critic's role clean and avoids it altering the work it judges | — | — | — |
| D5 | Run as a single quick check per action, on a cheaper/faster model than the worker | proposed | Controls the extra cost of adding a second reviewer | — | — | — |
| D6 | By default the reviewer sees only the last action plus the step, not full history | proposed | Less context is cheaper; revisit if judgment quality is poor (open question) | — | — | — |

## History log
- 2026-05-31 — feature created from STORY-011; decisions seeded from the story's behaviour and rubric, all proposed pending /feature decide.
