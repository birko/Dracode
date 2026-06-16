---
id: FEATURE-046
created: 2026-02-06
owner: human
status: done
---

# Network error handling for tasks

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
When the system talked to an AI provider and the connection failed, the task was sometimes marked as "Done" even though nothing was actually produced. That meant broken or empty work could quietly slip through and look like success, eroding trust in the pipeline's status reports.

## Proposed shape
When a provider returns an error response with no real content, the system now treats that as a genuine failure: it records the error and marks the worker's task as "Failed" instead of "Done". This makes status honest — a task only shows as complete when it truly produced a result. The behaviour applies consistently across every provider and every type of worker.

## Out of scope (initial)
- Automatic retry strategy changes (handled separately by the existing retry/backoff logic)
- Provider-specific error messages or custom remediation guidance

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
