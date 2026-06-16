---
id: FEATURE-057
created: 2026-02-03
owner: human
status: done
---

# Kobold implementation planner

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

When a worker picked up a task, it jumped straight into doing the work with no map. If it was interrupted partway through — a timeout, a crash, a restart — there was no record of how far it had gotten, so work was redone or lost. There was also no way to see, ahead of time, what a task was actually going to touch.

## Proposed shape

Before any code is written, a dedicated planner now breaks a task down into a list of concrete, ordered steps — each one naming the files it will create or change — sequenced so that dependencies come first. That plan is saved, so if a worker stops partway it can pick up exactly where it left off instead of starting over. How aggressively planning runs (how many planning passes, whether progress is saved, whether work resumes from a saved plan) is configurable per environment.

## Out of scope (initial)

- Replacing the worker that executes the steps — the planner only produces the plan.
- Cross-task or cross-project planning — each plan covers a single task.

## Prototype

- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
