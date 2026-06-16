---
id: FEATURE-031
created: 2026-05-31
owner: human
status: idea
---

# End-to-end task acceptance verification

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Today a piece of work is considered "finished" as soon as every individual step in its plan
has run. But "all the steps ran" is not the same as "the thing we asked for actually got
built." There is no final check that the completed work matches the acceptance criteria we
originally wrote down — so a task can quietly be marked done while missing something we
explicitly required.

## Proposed shape

When a worker finishes all the steps of a task, run one final acceptance check that compares
the produced result against the original acceptance criteria, target files, and promised
interfaces we recorded for that task. If it passes, the task is marked done. If it fails, the
task is not closed — instead we open a focused fix task naming exactly what was missed, the
same way the existing verification flow already handles fixes. For criteria that a machine
can't judge (e.g. "looks good"), the check hands off to a human review rather than guessing a
pass. The behaviour ships behind a flag, off until we've measured it.

## Out of scope (initial)

- Scoring individual actions/steps as they happen (covered by the separate critic-model epic).
- Build / test / lint / toolchain checks — already handled by the existing verification service;
  this check is about intent, not toolchain output.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
