---
id: STORY-032
parent: EPIC-016
status: planned
created: 2026-05-29
---

# Migrate remaining file-based working artifacts to the database

## User story

As the agent pipeline, I want my working artifacts (Wyrm recommendation, Wyvern analysis, planning context, notifications, plan copies) stored in the database so that they are transactional, queryable, and not lost when a project folder is moved or cleaned.

## Behaviour

- `wyrm-recommendation.json`, `analysis.json`, `planning-context.json`, and `notifications.json` are read from and written to dedicated DB entities/repositories (following the `SqlPlanRepository` / `SqlUsageRepository` pattern).
- Any `.md` files retained (`analysis.md`, `kobold-plans/*.md`) are regenerated *from* the DB record on demand and marked as non-authoritative views.
- On first load of a pre-existing on-disk project, the JSON files are imported into the DB once (idempotent), then the DB becomes source of truth.
- Writes are immediate/transactional (no debounce race), matching the resolution already applied to plans and Dragon history.
- Edge case: a project whose folder exists but whose DB rows are missing must self-heal by re-importing, not crash.
