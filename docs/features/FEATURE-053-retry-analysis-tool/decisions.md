---
id: FEATURE-053
created: 2026-02-04
---

# Retry analysis tool — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add a Warden tool to retry failed analysis, with list, retry, and status actions | approved | shipped in 2.4.1 | 2026-02-04 | team | — |
| D2 | Show a retry button in the web UI for projects whose analysis failed | approved | shipped in 2.4.1 | 2026-02-04 | team | — |
| D3 | Display the failure error to the user so failed projects are visible, not silently stalled | approved | shipped in 2.4.1 | 2026-02-04 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-04 — feature created; decisions seeded from the shipped 2.4.1 changelog entry that added retry-on-failure for Wyvern analysis.
