---
id: FEATURE-013
created: 2026-05-31
owner: human
status: idea
---

# Design EvaluatorAgent

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When an automated worker finishes a piece of work, it currently grades its own
homework — the same worker reflects on whether it did a good job. Because it's the
same "mind" with the same blind spots, it can confidently declare success even when
the work didn't actually land (for example, claiming a change was saved when nothing
really changed). We need a genuinely independent critic.

## Proposed shape

Introduce a separate, dedicated reviewer that looks at what the worker just did and
judges it against the agreed expectations for that step. The reviewer hands back a
clear verdict — approve or reject — plus a score, a short reason, a list of anything
still missing, and an optional suggested correction.

The reviewer never touches the work itself; it only observes and judges (read-only).
It runs as a single quick check per action and is expected to use a cheaper, faster
model than the worker, keeping cost in check.

## Out of scope (initial)

- The reviewer does not execute tools or make any changes (strictly read-only).
- Reviewing the supervisor's routing decisions (that logic is deterministic, not a worker).
- Reviewing how work is broken down into tasks in the first place (handled elsewhere).
- Deciding whether to ship the reviewer broadly — calibration is a separate effort.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
