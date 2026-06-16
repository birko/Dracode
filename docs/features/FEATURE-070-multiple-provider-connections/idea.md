---
id: FEATURE-070
created: 2026-01-14
owner: human
status: done
---

# Multiple connections to same provider

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
Users could only connect to a given provider once at a time. That made it impossible to run two parallel conversations with the same provider, compare how the same provider answered the same question twice, or A/B test different prompts side by side.

## Proposed shape
Let a user open several independent connections to the same provider at once. Each connection is its own agent instance with its own conversation history, so nothing bleeds between them. The interface auto-numbers the tabs (for example "OpenAI", "OpenAI #2") and shows how many connections are open, making it obvious which session is which.

## Out of scope (initial)
- Shared or merged history across the parallel connections
- Automatic comparison/diffing of responses (users compare manually)
- Limits on the number of simultaneous connections

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
