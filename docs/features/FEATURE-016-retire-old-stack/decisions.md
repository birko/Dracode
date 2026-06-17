---
id: FEATURE-016
created: 2026-05-31
---

# Retire DraCode.WebSocket and DraCode.Web — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Delete the old single-agent server and browser client from the filesystem and the solution | approved | Folders deleted; both projects dropped from DraCode.slnx | 2026-06-17 | human | TASK-029 |
| D2 | Remove the old stack from the orchestration setup and drop any shared bits only it used | approved | AppHost project references + AddProject blocks removed; ServiceDefaults untouched (shared) | 2026-06-17 | human | TASK-029 |
| D3 | Scrub README and docs of the retired endpoint, its port, and its run instructions | approved | README/docs/CLAUDE.md scrubbed; stale FULL_PROJECT_SPECIFICATION.md retired | 2026-06-17 | human | TASK-029, TASK-070 |
| D4 | Update the project structure overview and build commands to reflect the removal | approved | CLAUDE.md project table + "9 projects → 7" count + build commands updated | 2026-06-17 | human | TASK-029 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-014 behaviour bullets.
- 2026-06-17 — reconciled with task tree: D1–D4 proposed → approved (ratified by STORY-014 execution — both tasks shipped and verified); `→ Tasks` wired to TASK-029/070. Feature signed off → done (TASK-029 verified via the Aspire dashboard; pure removal, no further human surface).
