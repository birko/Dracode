---
id: FEATURE-035
created: 2026-05-31
---

# Migrate remaining file-based working artifacts to the database — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Store the pre-analysis recommendation, task analysis, coordination context, and notifications in dedicated database records instead of project-folder files | proposed | — | — | — | — |
| D2 | Any retained human-readable file (analysis, plan copies) is regenerated from the database record and marked as a non-authoritative view | proposed | — | — | — | — |
| D3 | On first load of an existing on-disk project, its files are imported into the database once (idempotent), after which the database is the source of truth | proposed | — | — | — | — |
| D4 | Writes are immediate and transactional with no debounce race, matching how plans and conversation history were already fixed | proposed | — | — | — | — |
| D5 | A project whose folder exists but whose database rows are missing self-heals by re-importing rather than crashing | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-032 behaviour bullets under EPIC-016.
