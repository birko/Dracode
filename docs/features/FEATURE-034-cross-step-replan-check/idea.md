---
id: FEATURE-034
created: 2026-05-31
owner: human
status: idea
---

# Cross-step coherence re-plan check

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

A worker's plan is written once, up front, and then locked. If an early step fails or finishes
differently than expected, the later steps keep running against assumptions that the early step
may have just invalidated — so step four can be carried out as if step two went a way it didn't.
Today the plan is only revisited when the whole task escalates, which is heavy and late.

## Proposed shape

Add a cheap coherence check that fires only when a step fails or is revised. It quickly asks: do
the remaining steps' assumptions — their target files, dependencies, and what earlier steps were
supposed to produce — still hold? In the common case they do, and execution continues untouched.
If a real contradiction is found, only the affected later steps are revised (reusing the existing
plan-revision machinery and keeping completed work), or it escalates if the change is large. The
key constraint is that this is a light gate, not a re-planning loop: it defaults to "continue"
unless it spots a concrete contradiction, and a step it just revised won't immediately re-trigger
another revision of the same steps. Ships behind a flag, off until measured for thrash.

## Out of scope (initial)

- Full re-planning loops — explicitly avoided; this is a cheap gate.
- Changes to escalation routing (separate epic).

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
