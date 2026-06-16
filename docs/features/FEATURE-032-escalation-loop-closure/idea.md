---
id: FEATURE-032
created: 2026-05-31
owner: human
status: idea
---

# Escalation loop closure — feed resolution back to the Kobold

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When a worker gets stuck and raises a problem upstream, a supervisor resolves it — by revising
the plan, refining the task, or reassigning it to a different specialist. But once that's done,
the worker simply resumes with no idea what changed or why. The feedback loop is open, so the
worker can walk straight back into the same mistake it just escalated.

## Proposed shape

Whenever the supervisor resolves an escalation, it writes back a short resolution note: what was
decided, what actually changed (the plan was revised / the task was refined / it was reassigned),
and plain guidance such as "the approach was changed because X — don't retry Y." On resume, that
note is dropped into the worker's context so its very next reasoning step takes the resolution
into account. It covers every kind of auto-resolved escalation, and when a human answers a parked
question, the human's answer travels back through the same channel. For reassignments, the note
briefs the new specialist on what the previous one tried and why it was wrong. Ships behind a flag,
off until measured.

## Out of scope (initial)

- The independent critic/evaluator model (separate epic).
- The underlying resume/injection plumbing if the human-check epic owns it — this feature
  supplies the *content* and reuses that channel.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
