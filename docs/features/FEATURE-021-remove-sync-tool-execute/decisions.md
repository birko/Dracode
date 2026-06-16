---
id: FEATURE-021
created: 2026-05-31
---

# Remove sync Tool.Execute() overloads — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Remove the old blocking tool entry point from the shared foundation and make the non-blocking path the only option | approved | shipped (TASK-018) | 2026-05-29 | ai | TASK-018 |
| D2 | Migrate every tool across both code bases to the non-blocking path and clear remaining blocking-call traces | approved | shipped (TASK-018) | 2026-05-29 | ai | TASK-018 |
| D3 | Apply the fix consistently across all three repositories rather than using a temporary shim | approved | shipped (TASK-018) | 2026-05-29 | ai | TASK-018 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from the standalone TASK-018 (already shipped); decisions recorded as approved.
