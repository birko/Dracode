---
id: FEATURE-033
created: 2026-05-31
owner: human
status: idea
---

# Budget- and cost-aware reflection

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When a worker pauses to ask itself "should I keep going or escalate?", it currently reasons only
about how far along it is and how confident it feels — it has no idea how much budget it has burned
or how much is left. The system's "you're running out of budget" warning is also crude: it just
counts how many times the worker has reflected, even though we already track the real token cost of
every call. So a worker can keep spending well past a sensible point before anything notices.

## Proposed shape

Feed the real numbers — tokens and estimated cost spent on this task, and how much budget remains —
into both the worker's self-reflection and the background monitor. The worker can then reason like
"I'm at 80% of this task's budget but only 40% done — time to escalate and split this," with evidence,
instead of waiting for a blunt iteration count. The monitor's budget warning is replaced with one
based on actual spend against the configured project/task budget. This is purely reasoning input —
the existing hard budget enforcement stays the authority on blocking calls. If cost tracking is turned
off, behaviour falls back cleanly to today's iteration-based logic. Ships behind a flag, off until
measured.

## Out of scope (initial)

- Changing how budgets are *enforced* (blocking calls) — that already exists and stays authoritative.
- New pricing or usage storage — reuses the existing usage records.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
