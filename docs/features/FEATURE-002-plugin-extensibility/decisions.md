---
id: FEATURE-002
created: 2026-05-31
---

# Plugin system for custom tools — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Discover and load plugins from a `plugins/` folder at startup | proposed | Lets third parties add tools without forking DraCode | — | — | TASK-003 |
| D2 | Load each plugin in isolation so it can be added or removed cleanly | proposed | Keeps plugins from interfering with each other or the core | — | — | TASK-003 |
| D3 | Show plugin-provided tools in the normal catalogue, clearly marked as from a plugin | proposed | Users see all tools in one place while knowing the source | — | — | TASK-003 |
| D4 | Require a metadata file per plugin (name, version, author, permissions) and allow disabling via config | proposed | Gives operators visibility and an off-switch without deleting files | — | — | TASK-003 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from EPIC-002 success criteria and the acceptance themes of TASK-003 (plugin system for custom tools).
