---
id: FEATURE-041
created: 2026-03-15
---

# Pipeline quality improvements — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Pre-analysis extracts specific languages, tech stack, constraints, and out-of-scope items (new Constraints/OutOfScope fields) | approved | shipped in Unreleased | 2026-03-15 | team | — |
| D2 | Task descriptions must include acceptance criteria, target file paths, and public API signatures | approved | shipped in Unreleased | 2026-03-15 | team | — |
| D3 | Every spec requirement maps to at least one task (requirements traceability) | approved | shipped in Unreleased | 2026-03-15 | team | — |
| D4 | Cross-module API extraction feeds the planner actual export signatures; mandatory execution rules and constraint propagation enforced | approved | shipped in Unreleased | 2026-03-15 | team | — |
| D5 | Post-task compilation verification runs critical checks (tsc / dotnet build) after completion | approved | shipped in Unreleased | 2026-03-15 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-03-15 — feature created; decisions seeded from delivered pipeline quality changes backfilled from CHANGELOG.
