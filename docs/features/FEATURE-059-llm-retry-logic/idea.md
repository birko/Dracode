---
id: FEATURE-059
created: 2026-02-03
owner: human
status: done
---

# LLM retry logic with backoff

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Calls to AI providers fail intermittently — rate limits, brief server errors, timeouts, flaky network. Previously a single hiccup could fail a whole task, even though retrying a moment later would have worked. That made long-running jobs fragile and wasted work.

## Proposed shape

Every provider call now retries automatically when it hits a recoverable error, waiting a little longer between each attempt (with a touch of randomness so many workers don't retry in lockstep). When a provider tells us how long to wait before trying again, we honour that. The retry behaviour — how many attempts, the starting delay, how fast it grows, whether to add jitter — is tunable, and the protection applies uniformly across all ten supported providers.

## Out of scope (initial)

- Retrying genuine, permanent errors (bad requests, auth failures) — only recoverable failures retry.
- Cross-provider failover — a failed call retries the same provider, it does not switch providers.

## Prototype

- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
