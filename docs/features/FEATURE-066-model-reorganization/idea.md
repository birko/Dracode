---
id: FEATURE-066
created: 2026-01-31
owner: human
status: done
---

# Model reorganization

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

The system's data definitions had all accumulated in one flat place, making the codebase harder to navigate as it grew. There was also a missing piece in the orchestration chain and configuration that had become more verbose than it needed to be.

## Proposed shape

Reorganize the data definitions into clearly themed groups — agents, configuration, projects, tasks, and websocket — so each area is easy to find. Add the missing orchestration component (a Wyrm factory) to complete the chain, and simplify the application's configuration so it is leaner and easier to maintain. This is primarily a tidy-up that improves long-term maintainability without changing user-facing behavior.

## Out of scope (initial)

- New end-user features or behavior changes
- Changes to how projects are processed

## Prototype

- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
