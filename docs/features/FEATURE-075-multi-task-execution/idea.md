---
id: FEATURE-075
created: 2026-01-20
owner: human
status: done
---

# Multi-task sequential execution

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
The agent could only run a single task per session. Users with several related jobs had to launch the agent over and over, with no shared progress view and no protection against one task's leftovers bleeding into the next.

## Proposed shape
Let users hand the agent a list of tasks and have it work through them one after another, starting each with a clean slate so tasks don't interfere with each other. Show running progress (task N of total), and keep going even if one task fails. Accept the task list in several convenient ways: comma-separated on the command line, multi-line when interactive, or as a structured list in config.

## Out of scope (initial)
- Running tasks in parallel (execution stays sequential)
- Sharing context between tasks (each task is isolated by design)

## Prototype
Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
