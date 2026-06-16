---
id: FEATURE-009
created: 2026-05-31
owner: human
status: idea
---

# Evaluate collapsing the three Drake background services

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Three separate background services run on their own timers to keep work flowing: one picks up ready projects and kicks off work, one watches for stuck workers and timeouts, and one detects stalled or looping reasoning and raises alerts. Their responsibilities overlap, and the last two are conceptually doing the same job — watching for trouble. More moving parts means more places to fail and more idle background activity. We want to know which of these can be merged without losing any of the safety they each provide.

## Proposed shape

First measure how much real work each service does versus how often it wakes up, finds nothing, and goes back to sleep. Then test merging the two "watcher" services into a single liveness service (lower risk first), and separately explore making the work-starter react to project-ready events instead of polling on a fixed timer. Each change gets a 24-hour soak test on a real project pipeline. The main risk is creating one large failure point by combining services, so any merger isolates each responsibility internally. We also document whether the current timer intervals were chosen deliberately or are arbitrary. The output is a clear recommendation on what to merge and what to leave alone.

## Out of scope (initial)

- The 23 specialized language workers (clearly differentiated by domain expertise)
- Resilience services (rate limiter, circuit breaker, cost tracker — infrastructure, not agent tiers)

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
