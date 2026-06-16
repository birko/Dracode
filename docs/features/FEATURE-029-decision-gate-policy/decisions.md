---
id: FEATURE-029
created: 2026-05-31
---

# Policy-driven decision gates (per-project) — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add a per-project decision-gate policy stored alongside the project's other settings, following the existing config layering | proposed | Operators need a per-project dial for oversight vs. autonomy | — | — | TASK-023 |
| D2 | Provide knobs: critical-tasks-require-approval, plan-revision approval, task-refinement approval, confidence threshold, allow-ask-human, max questions per task, decision timeout | proposed | Cover the meaningful oversight points raised in the story | — | — | TASK-023 |
| D3 | The supervisor consults the policy before choosing auto-resolve vs. routing to a human | proposed | The policy must actually drive the routing decision | — | — | TASK-023 |
| D4 | Empty/missing policy means exactly today's behaviour, proven by a regression test | proposed | No regression: autonomy stays the default | — | — | TASK-023 |
| D5 | Policy changes apply to new situations only and never retroactively unpark already-waiting tasks | proposed | Avoid surprising mid-flight behaviour changes | — | — | TASK-023 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-026; decisions seeded from the story's policy-knob bullets and its single task file (TASK-023 policy model + enforcement).
