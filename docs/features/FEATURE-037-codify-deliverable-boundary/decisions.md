---
id: FEATURE-037
created: 2026-05-31
---

# Codify and enforce the deliverable-vs-working-state boundary — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add a short rule to CLAUDE.md: deliverables (workspace code + git) and regenerable views stay as files; all other agent working state goes to the database | proposed | — | — | — | — |
| D2 | Update the "Data Storage Locations" map so each artifact is labelled DB (source of truth), file (deliverable), or file (regenerable view) | proposed | — | — | — | — |
| D3 | Add a lightweight check (doc note, code-review checklist item, or analyzer) that flags new file-based persistence of working state into project folders | proposed | — | — | — | — |
| D4 | Enumerate the intentional on-disk exceptions (workspace code, worktrees, generated markdown views) so the rule isn't read as "no files ever" | proposed | — | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-034 behaviour bullets under EPIC-016.
