---
id: FEATURE-017
created: 2026-05-31
---

# /kobold WebSocket endpoint — ad-hoc + project-scoped Kobold execution — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add a single worker endpoint with two modes selected by the opening message | approved | One endpoint, mode chosen by the opening message — matches STORY-015 protocol design | 2026-06-17 | human | TASK-038 |
| D2 | Ad-hoc mode validates the folder, auto-initialises version control if missing, and always works in an isolated copy | approved | Folder validation + auto-init + isolated workspace; needs path access widened for arbitrary cwds | 2026-06-17 | human | TASK-039, TASK-041 |
| D3 | Ad-hoc results are left for the user to review and merge themselves, not applied automatically | approved | Safer default; no surprise writes to the caller's folder | 2026-06-17 | human | TASK-039 |
| D4 | Project mode reuses the existing plan and isolated workspace, then commits to the project's feature branch | approved | Reuses the existing project pipeline; commits to the feature branch like normal execution | 2026-06-17 | human | TASK-040 |
| D5 | Stream live progress (start, tool calls, self-checks, completion, errors) reusing existing message shapes | approved | Per-run event source reusing existing message shapes for non-browser clients | 2026-06-17 | human | TASK-037 |
| D6 | Ad-hoc runs stay off the project registry, keyed only by run id, with cleanup after a retention window | approved | Keeps throwaway runs out of the registry; retention-window cleanup | 2026-06-17 | human | TASK-039 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-015 behaviour and open questions.
- 2026-06-17 — decide: D1–D6 proposed → approved (ratifying the STORY-015 design; work not yet started); `→ Tasks` wired to TASK-037…041 and feature back-link added to those tasks. Phase → building (0/5 done).
