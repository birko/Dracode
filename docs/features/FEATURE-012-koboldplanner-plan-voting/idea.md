---
id: FEATURE-012
created: 2026-05-31
owner: human
status: idea
---

# KoboldPlanner plan voting

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Before a worker executes a task, the system writes an implementation plan — the ordered
steps it will follow. Today that plan comes from a single attempt. A weak plan (steps that
bundle too much, run in the wrong order, or miss part of what the task asked for) makes the
worker churn, backtrack, or produce incomplete output. Because execution is the expensive
part, a small investment in producing a better plan up front can save a lot of wasted
execution time downstream.

## Proposed shape

Instead of one plan, the system generates two or three plan candidates in parallel for the
same task and scores each one: are the steps appropriately small, are they in a sensible
order, do they cover everything the task asked for, and is the overall step count
reasonable (not over- or under-broken-down). The highest-scoring plan wins. There is no
fall-back to merging plans — plans are intricate enough that stitching them together risks
inconsistent ordering, so the system simply picks the best whole candidate. Trivial,
low-complexity tasks can skip voting entirely since a single plan is fine. The behaviour is
off by default and the per-complexity threshold is configurable.

## Out of scope (initial)

- Merging or synthesising multiple plans into one (explicitly rejected as too risky).
- Voting on low-complexity tasks where a single plan is adequate.
- Voting on the code produced while executing the chosen plan.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
