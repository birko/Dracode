---
id: FEATURE-029
created: 2026-05-31
owner: human
status: idea
---

# Policy-driven decision gates (per-project)

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

The ability to stop and ask a human is only useful if you can control *when* it happens. Without a setting, you'd either gate everything (killing the autonomy that makes the system useful) or gate nothing. Operators need a dial: more oversight on critical or risky work, full autonomy for low-risk work.

## Proposed shape

A per-project policy that the supervisor consults before deciding whether to auto-resolve a problem or route it to a human. The policy offers several knobs: require approval for problems on critical-priority work, require a human to approve a proposed plan revision or task refinement before it's applied, park for a human when confidence drops below a threshold, cap how many questions an agent may ask per task, and set how long to wait before falling back to automatic handling. Crucially, every knob defaults to today's fully-automatic behaviour — so nothing changes unless an operator opts in. Policy changes apply to new situations only; they never retroactively unpark something already waiting.

## Out of scope (initial)

- A Dragon UI for editing the policy (initially configured via existing config/tools).
- Answering decisions in Dragon (separate feature).

## Prototype

Pending — backlog item; prototype decision deferred to /feature decide.
