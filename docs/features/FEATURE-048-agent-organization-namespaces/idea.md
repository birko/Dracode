---
id: FEATURE-048
created: 2026-02-06
owner: human
status: done
---

# Agent folder reorganization & namespaces

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
All 23 agents lived together in a single flat folder. As the catalogue grew, it became hard to find the right agent, see how they related, and tell coding agents from media agents at a glance.

## Proposed shape
The agents are now arranged into a clear hierarchy — shared base classes at the top, then groupings for coding agents, specialized coding agents, and media agents — with naming that matches the new structure. This makes the catalogue much easier to navigate and reason about. Importantly, the way the rest of the system creates agents is unchanged, so nothing downstream had to be touched.

## Out of scope (initial)
- Adding or removing any agents (this was purely a reorganization)
- Changing agent behaviour or the public way agents are created

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
