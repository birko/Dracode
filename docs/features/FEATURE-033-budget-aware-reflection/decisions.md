---
id: FEATURE-033
created: 2026-05-31
---

# Budget- and cost-aware reflection — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Expose real per-task spent tokens / estimated cost and remaining budget to the worker's reflection context | proposed | "Should I continue?" should reflect real cost, not an iteration count | — | — | TASK-027 |
| D2 | Let reflection factor budget into its decision (e.g. escalate to split earlier with "80% budget at 40% progress" evidence) | proposed | Earlier, evidence-backed escalation beats waiting for a blunt heuristic | — | — | TASK-027 |
| D3 | Replace/augment the monitor's budget-exhaustion check with real spend vs. the configured budget | proposed | The crude reflection-count heuristic ignores actual cost data we already have | — | — | TASK-027 |
| D4 | Keep this as reasoning input only — existing budget enforcement stays the authority on blocking calls | proposed | Avoid a second, conflicting enforcement path | — | — | TASK-027 |
| D5 | Fall back gracefully to today's iteration-based behaviour when cost tracking is disabled | proposed | Must not break when cost tracking is off | — | — | TASK-027 |
| D6 | Ship behind a feature flag, default off until measured | proposed | No behaviour change on healthy runs until proven | — | — | TASK-027 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-030; decisions seeded from the story behaviour and TASK-027 acceptance criteria, all proposed pending /feature decide.
