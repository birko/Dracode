---
id: FEATURE-058
created: 2026-02-03
---

# Allowed external paths — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Allow per-project access to directories outside the workspace via an explicit allowlist | approved | shipped in 2.4.0 | 2026-02-03 | team | — |
| D2 | Provide a management surface to list, add, and remove allowed external paths | approved | shipped in 2.4.0 | 2026-02-03 | team | — |
| D3 | Validate every file operation against the workspace plus approved paths before running | approved | shipped in 2.4.0 | 2026-02-03 | team | — |
| D4 | Store the allowlist as part of each project's configuration | approved | shipped in 2.4.0 | 2026-02-03 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-03 — feature created; decisions seeded from the shipped 2.4.0 changelog entry.
