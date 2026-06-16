---
id: FEATURE-036
created: 2026-05-31
owner: human
status: idea
---

# Persist structured decision & reasoning records

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When the agent pipeline makes a choice — picks an approach, reflects on its progress, decides to escalate a stuck task — that reasoning lives only in the moment and then disappears. There is no durable record of *why* the system did what it did. That makes it impossible to audit a run after the fact, to learn from past runs, or to let later review/voting steps look back at the reasoning that led to a result.

## Proposed shape

Record every significant agent decision and reflection as a structured row capturing which project, task, and agent it came from, when it happened, what kind of step it was (a decision, a reflection, an escalation, or a review verdict), the reasoning itself, the confidence, and what artifact it concerns. The self-assessment the agents already produce gets saved here instead of being thrown away. These records become queryable per project, task, and agent, feed cross-project learning, and act as the foundation that future consensus and review features read from and write their verdicts back into. High-volume reasoning is written without slowing down the main work.

## Out of scope (initial)

- The consensus/voting and evaluator mechanisms themselves — owned by separate epics; this feature only provides the persisted reasoning substrate they consume and emit.
- The generated code in `workspace/` and its git history — that remains the deliverable on disk.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
