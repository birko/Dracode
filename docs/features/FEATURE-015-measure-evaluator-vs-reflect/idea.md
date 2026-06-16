---
id: FEATURE-015
created: 2026-05-31
owner: human
status: idea
---

# Measure: does external evaluator catch failures the reflect tool misses?

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Adding an independent reviewer roughly doubles the cost of each task, and a reviewer
that is too strict creates churn while one that is too lenient catches nothing. Before
we turn it on, we need evidence that it actually adds value over the worker's existing
self-check — not just an opinion.

## Proposed shape

Replay 50-100 past tasks — a mix of clean successes, known silent failures, jobs that
hit the "wrong approach" escalation, and jobs a human later flagged as wrong. For each,
run the new reviewer and record its verdicts, then compare them against what actually
happened.

The headline measures are how often the reviewer correctly caught real failures versus
how often it wrongly rejected good work. We compare these against the worker's existing
self-check to see whether the reviewer adds genuine coverage or just repeats it. Clear
thresholds decide the outcome: strong catch rate with few false alarms → turn it on for
the most important tasks; too many false alarms → drop it; in between → refine the
reviewer's rubric. The deliverable is a decision memo plus the saved dataset.

## Out of scope (initial)

- Building the reviewer or wiring it in (separate features handle those).
- Deciding the final rollout beyond the measured recommendation in the memo.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
