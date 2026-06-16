---
id: FEATURE-042
created: 2026-02-26
---

# Specification version tracking — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Specs carry a version number plus a SHA-256 content hash | approved | shipped | 2026-02-26 | team | — |
| D2 | Workers capture the spec version when assigned and detect drift before execution, auto-reloading context on change | approved | shipped | 2026-02-26 | team | — |
| D3 | Tasks record which spec version they were created for | approved | shipped | 2026-02-26 | team | — |
| D4 | New view_specification_history tool exposes version history to the requirements owner | approved | shipped | 2026-02-26 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-26 — feature created; decisions seeded from delivered spec-versioning changes backfilled from CHANGELOG.
