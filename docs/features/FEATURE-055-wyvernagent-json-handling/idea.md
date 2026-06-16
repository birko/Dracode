---
id: FEATURE-055
created: 2026-02-04
owner: human
status: done
---

# Robust WyvernAgent JSON handling

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
The Wyvern analyzer received responses from the language model in several different shapes depending on the provider. When a response came back in an unexpected form, or was empty, the system would quietly produce an empty result instead of flagging that something had gone wrong — making failures hard to notice and harder to diagnose.

## Proposed shape
Make Wyvern's response parsing tolerant of every shape a provider might return — plain text, structured content blocks, lists of those, and various object forms — so valid analysis is always extracted. Just as important, when a response truly is empty or contains no usable result, raise a clear error instead of silently returning nothing. Failures become loud and obvious rather than invisible.

## Out of scope (initial)
- Changing what the model is asked to produce.
- Retrying or recovering from a bad response (just reporting it clearly).
- Parsing logic for agents other than Wyvern.

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
