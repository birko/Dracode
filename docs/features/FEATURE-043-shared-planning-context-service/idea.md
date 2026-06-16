---
id: FEATURE-043
created: 2026-02-09
owner: human
status: done
---

# Shared planning context service

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
Multiple automated workers run at the same time, but they had no shared awareness of each other. Two workers could touch the same file at once, duplicate effort already done by another, or repeat mistakes that an earlier run had already learned to avoid. Each project also started from scratch with no benefit from past experience.

## Proposed shape
Introduce a shared coordination and learning layer that all workers plug into. It tracks which workers are active, flags when two of them want the same file, and surfaces related plans so work isn't duplicated. It gives the supervisor a live view of worker lifecycle, heartbeats, and project statistics. Over time it learns from finished tasks — recording how long things took, what worked, and good patterns — so future tasks and even other projects benefit. Everything is kept thread-safe and saved alongside the project.

## Out of scope (initial)
- Replacing the per-project task tracking
- Cross-organization learning beyond the local project set
- Real-time human dashboards (handled elsewhere)

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
