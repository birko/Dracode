---
id: FEATURE-017
created: 2026-05-31
---

# /kobold WebSocket endpoint — ad-hoc + project-scoped Kobold execution — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add a single worker endpoint with two modes selected by the opening message | proposed | — | — | — | — |
| D2 | Ad-hoc mode validates the folder, auto-initialises version control if missing, and always works in an isolated copy | proposed | — | — | — | — |
| D3 | Ad-hoc results are left for the user to review and merge themselves, not applied automatically | proposed | — | — | — | — |
| D4 | Project mode reuses the existing plan and isolated workspace, then commits to the project's feature branch | proposed | — | — | — | — |
| D5 | Stream live progress (start, tool calls, self-checks, completion, errors) reusing existing message shapes | proposed | — | — | — | — |
| D6 | Ad-hoc runs stay off the project registry, keyed only by run id, with cleanup after a retention window | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-015 behaviour and open questions.
