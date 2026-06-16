---
id: FEATURE-049
created: 2026-02-04
owner: human
status: done
---

# Parallel execution & threading

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
The automated pipeline processed work one item at a time. Projects, supervisors, and tasks were handled sequentially, so a queue of waiting projects or tasks moved slowly even when the system had plenty of spare capacity. Throughput was limited not by available resources but by the one-at-a-time design.

## Proposed shape
Let the background services do many things at once instead of marching through them in single file. The task supervisor now handles multiple projects, supervisors, and tasks in parallel (while still respecting the per-project worker limits), the analysis service processes assignment, analysis, and re-analysis concurrently, and the monitoring service watches all supervisors at the same time. In practice this means roughly 4–8x faster task processing and 3–5x faster analysis, with no change to how a user starts or tracks a project.

## Out of scope (initial)
- Changing the per-project worker (Kobold) parallel limits themselves
- User-facing controls to tune the parallelism levels

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
