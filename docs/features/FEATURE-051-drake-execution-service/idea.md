---
id: FEATURE-051
created: 2026-02-04
owner: human
status: done
---

# Drake execution service

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
Once Wyvern had analyzed a project and broken it into tasks, nothing automatically carried that work forward into actual execution. Someone or something had to manually nudge analyzed projects into the build phase, leaving a gap in the promise of end-to-end automation.

## Proposed shape
A background service that wakes up on a short interval, finds projects that have finished analysis, and drives them to completion on its own. For each analyzed project it sets up the supervisors (Drakes) per task file, hands ready tasks to the workers (Kobolds), and marks the project complete once everything is done. This closes the loop so a request flows automatically from Dragon to Wyvern to Drake to Kobold with no manual handoff.

## Out of scope (initial)
- Manual task scheduling or hand-picking which task runs next.
- Changing how analysis itself is produced.
- User-facing controls for pausing or steering execution mid-run.

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
