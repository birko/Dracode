---
id: FEATURE-077
created: 2026-06-17
owner: human
status: done
---

# Unify Birko framework source compilation into DraCode.Birko

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

The shared framework the whole system is built on was being compiled into several
projects at the same time. The build only worked by accident — a compiler quirk quietly
hid the duplication — and the duplication had started to block new work: the database-backed
sign-in storage couldn't even be built, because the same underlying type existed in two
separate places and the two copies didn't count as "the same thing".

## Proposed shape

Make one project the single place that compiles the shared framework, and have every other
project consume that one compiled output instead of re-compiling the sources themselves. This
removes the duplication for good and unblocks the sign-in storage work. It was verified by a
clean build (the duplicate-compilation warnings dropped from over a thousand to zero) and a
full green test run.

## Out of scope (initial)

- The database-backed sign-in storage itself (the separate task this unblocks)
- The command-line tool (already a clean consumer, no change needed)
- Any change to what the framework actually does — this is purely about where it's compiled

## Prototype
- Skipped — shipped; the clean build and green test run are the proof.
