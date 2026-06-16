---
id: FEATURE-044
created: 2026-02-09
owner: human
status: done
---

# Wyrm pre-analysis workflow

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
Previously the system jumped straight from an approved specification into a detailed task breakdown. Without an upfront read of the spec, the breakdown stage had to guess at the languages, technologies, and overall complexity, which led to weaker plans and rework.

## Proposed shape
Add a lightweight pre-analysis pass that runs first, as soon as a spec is approved. It reads the specification and produces a recommendation covering the likely languages, the kinds of workers needed, the tech stack, and an estimate of complexity. This recommendation is saved and handed to the detailed breakdown stage, which now starts from an informed position rather than a blank page. The result is a two-phase analysis: a quick read-and-recommend, followed by the detailed task breakdown.

## Out of scope (initial)
- Replacing the detailed breakdown stage
- Generating actual code during pre-analysis
- User interaction during the pre-analysis pass

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
