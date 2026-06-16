---
id: FEATURE-053
created: 2026-02-04
owner: human
status: done
---

# Retry analysis tool

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
When a project's analysis failed, there was no clean way to see that it had failed or to try again. A failed project simply stalled, with no visible error and no path back into the pipeline.

## Proposed shape
A tool, available through the Warden assistant, that can list failed analyses, show their status, and retry them on demand. The web interface gains a retry button and a clear error display for failed projects, so a user can spot a failure and re-run it without manual intervention behind the scenes.

## Out of scope (initial)
- Automatic, unattended retry of failures (this is user-initiated).
- Retrying anything other than the analysis step.
- Diagnosing the root cause of the failure.

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
