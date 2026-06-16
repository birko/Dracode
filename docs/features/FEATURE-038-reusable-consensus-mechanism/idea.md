---
id: FEATURE-038
created: 2026-05-31
owner: human
status: idea
---

# Reusable ConsensusService abstraction

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When the system wants to double-check an important automated decision, it currently re-builds the same "ask several times and agree" machinery from scratch every time. There are already three separate hand-built copies of this logic, each slightly different. There is no single, shared way for any part of the system to say "get me a second opinion on this decision," which makes every new high-stakes check expensive to add and easy to get subtly wrong.

## Proposed shape

Provide one reusable consensus building block. A part of the system that wants a cross-check hands it a way to produce candidate answers, a way to score them, and a rule for picking a winner (for example: go with the majority, pick the best-scoring among those that agree, or — when answers diverge — ask again with all options on the table). It hands back a clear result: the chosen answer, how each candidate scored, how much agreement was reached, and a plain rationale that gets filed as a reasoning record. It can also run in a predictable "test" mode where every run returns the same answer, so automated tests stay stable, and it respects an on/off switch so that when consensus is turned off it simply runs once with no extra cost.

## Out of scope (initial)

- The specific existing voting integrations and the single-critic evaluator design — those live in their own efforts; this only builds the shared machinery.
- The diverse-panel mode (different specialist agents) — that builds on top of this and is tracked separately.
- Human-in-the-loop approval gates — consensus here is agent-to-agent; human escalation is a separate tier.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
