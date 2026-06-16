---
id: FEATURE-074
created: 2026-01-12
owner: human
status: done
---

# v2.0.2 bug fixes & debug tooling

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
After the client modernization, the provider grid failed to display because the server and client disagreed on how property names were capitalized. There was also little tooling to diagnose why messages between the client and server were not behaving as expected.

## Proposed shape
Fix the naming mismatch so the provider grid renders correctly, and add debugging support to make message issues easy to inspect: richer logging, a dedicated inspector page, and console commands for live diagnosis.

## Out of scope (initial)
- New user-facing features beyond the fix and the diagnostic tooling
- Changes to the underlying provider list itself

## Prototype
Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
