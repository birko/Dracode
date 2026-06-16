---
id: FEATURE-047
created: 2026-02-06
owner: human
status: done
---

# OrchestratorAgent base class

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
The three coordinating agents (Dragon, Wyrm, Wyvern) each repeated the same boilerplate logic for things like guidance, reasoning style, and reading responses. That duplication made the agents harder to maintain and easy to let drift apart over time.

## Proposed shape
A new shared foundation for orchestrating agents gathers the common behaviour in one place: standard coordination guidance, reasoning-depth tuning, and reliable parsing of agent responses. Dragon, Wyrm, and Wyvern now build on this foundation instead of carrying their own copies, so the agents stay consistent and future changes happen once rather than three times.

## Out of scope (initial)
- Changes to what each orchestrator actually decides (behaviour preserved, only the plumbing was consolidated)
- The specialist worker agents, which keep their own base classes

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
