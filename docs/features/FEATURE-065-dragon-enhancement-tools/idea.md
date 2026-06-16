---
id: FEATURE-065
created: 2026-01-31
owner: human
status: done
---

# Dragon enhancement tools (import & approval)

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Two gaps slowed teams down. First, there was no way to bring an existing codebase into the system — every project had to start from scratch. Second, specifications flowed straight into automated processing with no human checkpoint, so a half-finished or incorrect spec could trigger work prematurely.

## Proposed shape

Add an import tool that scans an existing codebase, auto-detects its technology, and generates an initial specification so existing projects can join the pipeline. Add an approval tool that introduces a two-stage gate — a specification moves from Prototype to Approved, and only Approved specs are picked up for automated breakdown and execution. This gives a deliberate human sign-off before downstream work begins.

## Out of scope (initial)

- Automatic migration or refactoring of imported code
- Multi-reviewer or sign-off workflows beyond a single approval step

## Prototype

- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
