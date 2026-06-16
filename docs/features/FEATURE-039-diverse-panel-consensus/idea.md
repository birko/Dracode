---
id: FEATURE-039
created: 2026-05-31
owner: human
status: idea
---

# Diverse-panel consensus (heterogeneous agents)

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Today's cross-checks either ask the same kind of expert the same question several times, or use a single dedicated critic. Both miss problems that a different kind of expert would immediately spot. For a high-stakes output — say a generated piece of code — one perspective might confirm it works while completely missing that it's poorly designed or impossible to test. We have no way to convene a panel of genuinely different specialists and require them to agree before an output is accepted.

## Proposed shape

Build a "panel review" on top of the shared consensus building block. Instead of running one specialist several times, it convenes several different specialists, each looking through its own lens (for a generated code module, for example: one checks correctness, another checks design and duplication, another checks testability). Each panellist returns a clear verdict — approve or reject, a score, a reason, and any concerns — and an agreement rule combines these diverse views (for instance, accept only if no panellist rejects on its own dimension). Every panel review is filed as a reasoning record that captures each member's individual view, not just the final call. Because it costs several reviews per check, it stays behind an on/off switch and a priority threshold so it only runs on the most critical work, and it is bounded so it can't loop forever — after a set number of rounds it hands off to a human or accepts with a warning.

## Out of scope (initial)

- Same-kind repeated voting and the single-critic evaluator — those are separate, narrower approaches.
- The shared consensus machinery itself — this feature consumes it rather than building it.
- Human approval gates — referenced only as the fall-through when the panel can't settle.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
