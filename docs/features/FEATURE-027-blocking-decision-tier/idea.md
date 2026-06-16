---
id: FEATURE-027
created: 2026-05-31
owner: human
status: idea
---

# Blocking "AwaitingHumanDecision" escalation tier

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When a piece of work runs into a wall, the system today fixes the problem with the same automatic judgment that caused it — there's no way to stop and ask a person. The whole project is autonomous, so a genuinely consequential call gets auto-resolved silently and the human only finds out afterwards by checking notifications. That means the same kind of reasoning that produced a bad outcome is also the only thing allowed to correct it.

## Proposed shape

Add a way for a single task to "park" itself and wait for a human's decision when its situation is too important to auto-resolve. The parked task stops, but the rest of the project keeps running other work normally — so one waiting task never freezes the whole project. The pending question is saved with enough structure to survive a server restart, and once a person answers, the task picks back up exactly where it left off. As a safety net, if nobody answers within a configurable time limit, the task falls back to today's automatic behaviour so it can never wait forever.

## Out of scope (initial)

- The logic that *decides when* a task should wait for a human (that's the policy feature).
- The agent-side tool that lets an agent ask a question (separate feature).
- The Dragon chat experience for showing and answering the decision.

## Prototype

Pending — backlog item; prototype decision deferred to /feature decide.
