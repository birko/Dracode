---
id: FEATURE-014
created: 2026-05-31
owner: human
status: idea
---

# Integrate EvaluatorAgent into the Kobold tool loop

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Having an independent reviewer is only useful if its feedback actually reaches the
worker while it is still working. Right now there is no point in the worker's loop
where an outside opinion is injected — so even a good critic would have no effect.
We need to wire the reviewer's verdict back into the worker's next move.

## Proposed shape

After each meaningful action the worker takes (anything that changes files or saves
work), the reviewer scores it. If the reviewer rejects the action, its reason and
suggested fix are handed back to the worker before it decides what to do next, so the
worker can course-correct. Purely look-only actions are skipped to avoid wasted cost.

If the reviewer rejects three actions in a row on the same step, the work is escalated
through the existing "wrong approach" path so a human or upstream planner can step in.
The whole feature ships behind an off-by-default switch, with cost controls: a cheaper
reviewer model, skipping low-priority work, and a configurable "review every N actions"
cadence.

## Out of scope (initial)

- Designing the reviewer itself (covered by the separate EvaluatorAgent feature).
- Turning the reviewer on in production before its accuracy is measured.
- Reviewing read-only actions (looking at files) — skipped to keep cost down.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
