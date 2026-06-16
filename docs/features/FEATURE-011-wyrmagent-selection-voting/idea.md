---
id: FEATURE-011
created: 2026-05-31
owner: human
status: idea
---

# WyrmAgent agent-type selection voting

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Each task is handed to a specialist worker chosen for its primary technology — a C#
specialist, a React specialist, and so on. That choice is made by a single decision today.
When a task's technology is ambiguous, the system often picks the generalist or guesses
wrong, and a wrong specialist assignment surfaces later as a failed handoff that has to be
escalated and reassigned — wasted time and churn. We want the wrong-specialist rate to
drop without paying extra on the many tasks where the right choice is obvious.

## Proposed shape

The system makes its specialist choice once, as it does today. It only spends more effort
when the choice looks uncertain — for example when it falls back to the generalist, or
when the task mentions two or more technologies. In those uncertain cases it runs two more
selections in parallel and takes a simple majority vote. If the votes are tied or split
three ways, it defaults to the generalist so the task is never blocked. Tasks with a clear
single-technology assignment stay at a single selection, so the extra cost only lands where
it actually helps. The behaviour ships behind an off-by-default switch with a configurable
ambiguity threshold.

## Out of scope (initial)

- Voting on every task — only ambiguous ones trigger extra selections.
- Voting on the actual code the chosen specialist writes.
- Changing the catalogue of available specialist types.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
