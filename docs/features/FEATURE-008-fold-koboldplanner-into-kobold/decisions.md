---
id: FEATURE-008
created: 2026-05-31
---

# Evaluate folding KoboldPlanner into Kobold — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run an A/B comparison of plan-then-execute (separate planner) vs. a worker that plans on its own first iteration | proposed | The worker's periodic self-assessment may already do the planner's thinking; needs evidence | — | — | — |
| D2 | Score both variants on cost per task, plan quality, step-completion rate, escalation rate, and resumability after an intentional restart | proposed | These determine whether the separate planning stage earns its cost beyond plan persistence | — | — | — |
| D3 | Ensure the worker-only variant receives the same rich context the dedicated planner gets today (related plans, similar-task insights, best practices) | proposed | Without equivalent context, worker-only plans degrade and the comparison is unfair | — | — | — |
| D4 | Decide whether the dedicated re-planning step can be replaced by the worker re-planning itself after getting stuck, or is still needed for non-worker escalations | proposed | External re-planning may still be valuable for escalations that do not originate from the worker | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-006 hypothesis, test plan, and open questions.
