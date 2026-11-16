---
id: FEATURE-031
created: 2026-05-31
---

# End-to-end task acceptance verification — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run a whole-task acceptance check after all steps complete, before marking the task done | proposed | "All steps ran" must not masquerade as "task accomplished" | — | — | TASK-025 |
| D2 | Check the result against the recorded acceptance criteria / target files / promised interfaces, not just "file exists and is non-empty" | proposed | Intent-level verification, distinct from per-step file checks | — | — | TASK-025 |
| D3 | On failure, don't mark done — open a focused fix task naming the unmet criteria, reusing the existing fix-task pattern | proposed | Failures should be surfaced and routed, not silently passed | — | — | TASK-025 |
| D4 | Criteria a machine can't judge degrade to a human review instead of auto-passing | proposed | Avoid false "done" on subjective criteria; ties into the human-check epic | — | — | TASK-025 |
| D5 | Ship behind a feature flag, default off until measured | proposed | No throughput regression on healthy tasks until proven | — | — | TASK-025 |
| D6 | An LLM judges prose criteria first, so only the ones it cannot decide reach a human | proposed | Without it every task with prose criteria waits for a person | — | — | TASK-165 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-028; decisions seeded from the story behaviour and TASK-025 acceptance criteria, all proposed pending /feature decide.
- 2026-11-16 — D6 proposed — TASK-165 spawned from TASK-025 (LLM judge for prose criteria, found while planning). TASK-025 now waits on EPIC-014 (owner, 2026-11-16): criterion 4's human check needs answerable notifications, and no fallback is wanted. Needs /feature decide.
