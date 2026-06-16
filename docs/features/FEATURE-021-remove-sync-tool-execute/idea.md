---
id: FEATURE-021
created: 2026-05-31
owner: human
status: done
---

# Remove sync Tool.Execute() overloads

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Every tool the agents use had two ways to run: an older blocking version and a newer non-blocking one. The live system already used the non-blocking path, but the old blocking version still existed as dead code. It was a trap — any future contributor who reached for it would quietly reintroduce a performance problem that can starve the system under load.

## Proposed shape

Remove the old blocking entry point from the shared tool foundation and from every tool that still implemented it, leaving only the safe non-blocking path. This closes the trap for good: there is no longer any way to call the old version because it no longer exists. The change spanned the shared contract and two code bases, and was verified with a clean build and checks confirming no traces of the old pattern remained.

## Out of scope (initial)

- Reworking unrelated blocking calls elsewhere in the system (separate cleanup)
- Any change to what individual tools actually do
- Wider changes to the shared tool foundation beyond this one method

## Prototype
- Skipped — shipped; the running pipeline is the proof.
