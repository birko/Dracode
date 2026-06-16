---
id: FEATURE-045
created: 2026-02-09
owner: human
status: done
---

# Agent creation pattern consistency

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
Different parts of the system created their automated workers in inconsistent ways. Some went through a shared factory while others built workers directly, which meant provider settings (which model, which service) were not applied uniformly. This made behaviour harder to predict and configuration changes easy to miss.

## Proposed shape
Audit every place that creates a worker and standardize on a single shared factory so provider configuration is applied the same way everywhere. The supervisor, the breakdown stage, the workers, and the pre-analysis stage all go through the common factory. The few places that legitimately build a worker directly are documented with a clear justification so the exceptions are intentional and visible.

## Out of scope (initial)
- Changing what the workers actually do
- Adding new worker types
- Runtime provider switching at the user level

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
