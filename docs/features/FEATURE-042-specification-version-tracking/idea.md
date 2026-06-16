---
id: FEATURE-042
created: 2026-02-26
owner: human
status: done
---

# Specification version tracking

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
While a project was being built, its specification could quietly change underneath the workers. There was no way to tell which version of the spec a piece of work was based on, so projects could drift away from the current requirements without anyone noticing.

## Proposed shape
Give every specification a version number and a content fingerprint. When a worker is assigned a task, it records the spec version it is working from. Before doing the work, it checks whether the spec has changed and, if so, refreshes its understanding automatically. Tasks remember which version they were created for, and a new history tool lets the requirements owner review how the spec evolved over time. This keeps active work aligned with the latest agreed requirements.

## Out of scope (initial)
- Merging or reconciling conflicting spec edits from multiple people
- Notifying stakeholders of every spec change
- Versioning of generated code artifacts

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
