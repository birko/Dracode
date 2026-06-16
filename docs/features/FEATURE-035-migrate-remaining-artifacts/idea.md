---
id: FEATURE-035
created: 2026-05-31
owner: human
status: idea
---

# Migrate remaining file-based working artifacts to the database

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

The agent pipeline still scatters important working data across loose files in each project folder — the pre-analysis recommendation, the task breakdown, the coordination context, and pending notifications all live as standalone files. If a project folder is moved, cleaned up, or partially loses some of those files, that working state is gone or the system can misbehave. It is also hard to query across projects when everything is locked away in per-folder files.

## Proposed shape

Move the remaining working-state files into the shared database, the same way plans, conversation history, and cost tracking already moved. Each artifact gets its own durable, transactional home in the database so writes can't race or be lost. Any human-readable file that stays behind becomes a regenerated "view" of the real record, not the source of truth. Existing projects on disk are imported into the database automatically the first time they load, and a project whose database rows are missing quietly re-imports itself instead of failing.

## Out of scope (initial)

- The generated code under `workspace/` and its git history — that is the deliverable and stays on disk.
- Human-readable exports such as analysis and plan markdown — they may remain, but only as regenerable views, not the source of truth.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
