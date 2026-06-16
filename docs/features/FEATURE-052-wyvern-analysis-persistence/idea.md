---
id: FEATURE-052
created: 2026-02-04
owner: human
status: done
---

# Wyvern analysis persistence

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
Wyvern's analysis of a project lived only in memory. If the server restarted, that work was lost and the project had to be re-analyzed, wasting time and burning unnecessary LLM cost.

## Proposed shape
Save each project's analysis to a durable file as soon as it is produced, and load it back when needed. On startup the system can recover any analysis that was already done, so a restart no longer means starting analysis over. The analysis becomes a recoverable, persistent artifact of the project.

## Out of scope (initial)
- Versioning or history of past analyses.
- Editing the persisted analysis by hand.
- Persisting in-progress (partial) analysis.

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
