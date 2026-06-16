---
id: FEATURE-061
created: 2026-02-01
---

# Git integration — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Make every project a real Git repository with init, branch, commit, and merge handled by a shared service | approved | shipped in 2.3.0 | 2026-02-01 | team | — |
| D2 | Give each feature its own branch and auto-commit completed work as it lands | approved | shipped in 2.3.0 | 2026-02-01 | team | — |
| D3 | Provide branch status visibility: current branch, unmerged feature branches, and merge readiness | approved | shipped in 2.3.0 | 2026-02-01 | team | — |
| D4 | Detect conflicts during merge before combining feature work back into the main line | approved | shipped in 2.3.0 | 2026-02-01 | team | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-02-01 — feature created; decisions seeded from the shipped 2.3.0 Git integration work (GitService, git_status / git_merge tools, Drake auto-commit, Wyvern feature branches).
