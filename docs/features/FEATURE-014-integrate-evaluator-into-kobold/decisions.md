---
id: FEATURE-014
created: 2026-05-31
---

# Integrate EvaluatorAgent into the Kobold tool loop — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run the reviewer after each action that changes files or saves work; skip look-only actions | proposed | Feedback is only useful on real changes; skipping reads keeps cost down | — | — | — |
| D2 | On a reject, feed the reason and suggested fix back to the worker before its next step | proposed | Closes the loop so the worker can course-correct in real time | — | — | — |
| D3 | Three rejects in a row on the same step escalate via the existing "wrong approach" path | proposed | Stops endless thrash and hands off to existing escalation handling | — | — | — |
| D4 | Ship behind an off-by-default switch with a per-priority threshold | proposed | Lets us measure before turning it on; avoids risk in production | — | — | — |
| D5 | Cost controls: cheaper reviewer model, skip low-priority work, configurable review cadence | proposed | Adding a reviewer roughly doubles cost; controls keep it affordable | — | — | — |

## History log
- 2026-05-31 — feature created from STORY-012; decisions seeded from the story's flow, abort condition, feature flag, and cost mitigations, all proposed pending /feature decide. Depends on FEATURE-013 shipping first.
