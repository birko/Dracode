---
id: FEATURE-007
created: 2026-05-31
owner: human
status: idea
---

# Evaluate merging Wyrm + Wyvern into one analyzer

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When a new project specification arrives, two separate analysis agents run back-to-back before any work starts: one does a quick "pre-analysis" pass and the other does the detailed task breakdown. Both read the very same specification and both produce structured output. That means every project pays for two analysis runs (and two billable AI calls) before a single line of work begins. The split between them is conceptual rather than technical, so it is worth asking whether we are paying real money for a separation that does not pull its weight.

## Proposed shape

Run a head-to-head comparison between today's two-step analysis and a single combined analyzer that produces everything in one pass. We would test both approaches on a handful of representative specifications and score them on whether every requirement still ends up covered, whether project constraints still reach the workers, total cost per spec, time to reach a ready-to-build state, and how often workers later get stuck (a proxy for analysis quality). The output is a clear decision — merge, keep separate, or a hybrid — backed by evidence, not a blind merge.

## Out of scope (initial)

- The 23 specialized language workers (clearly differentiated by domain expertise)
- Resilience services (rate limiter, circuit breaker, cost tracker — infrastructure, not agent tiers)

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
