---
id: FEATURE-010
created: 2026-05-31
owner: human
status: idea
---

# Wyvern task breakdown voting

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When the system breaks a project specification down into tasks, it relies on a single
analysis pass. If that one pass gets the breakdown wrong — misses a requirement, splits
the work badly, or drops a constraint — every later stage inherits the mistake. Planning,
execution, verification, and commits all build on the flawed breakdown, so one bad call
quietly wastes the entire downstream effort. This is the single most cascading decision in
the whole pipeline, yet today it has no safety net.

## Proposed shape

Instead of trusting one analysis, the system runs the task-breakdown step three times in
parallel on the same specification and compares the results. Each candidate is scored on
how well it covers the requirements, carries through the project's constraints, assigns
valid specialist types, and writes clear task descriptions. If two of the three agree on
the overall shape, the higher-scoring of those two wins. If all three disagree, a fourth
pass is asked to synthesise a single best breakdown from the three candidates. The whole
behaviour sits behind an off-by-default switch so teams can compare it against today's
single-pass behaviour and turn it on only where the quality gain is worth the extra cost.

## Out of scope (initial)

- Voting on the actual code Kobolds write (verifiable by build/test; voting adds noise).
- Voting on task execution order (already deterministic by priority and dependencies).
- Changing how the breakdown itself is produced — only how many times and how it is chosen.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
