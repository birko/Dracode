---
id: FEATURE-005
created: 2026-05-31
owner: human
status: done
---

# Structured reasoning tools

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Long-running automated workers (Kobolds) can quietly get stuck — repeating the same failing step, making no real progress, or losing confidence — without anyone noticing until time and budget are wasted. Earlier, the only signal was free-text notes inside prompts, which were hard to analyze or act on automatically.

## Proposed shape

Give workers an explicit check-in tool that captures their state as structured fields — how far along they are, what's blocking them, how confident they are, and what they plan to adjust. When confidence drops too low or progress stalls across several check-ins, the supervisor (Drake) is automatically pulled in. Separately, an independent monitor service watches worker output for stuck-loop and repeated-error patterns the worker itself might not flag, and recommends intervention. Both work alongside the existing prompt-based self-reflection approach.

## Out of scope (initial)

- LLM-side reasoning quality improvements (prompt engineering — separate)
- Machine-learning-based pattern detection (heuristic rules in v1)
- Cross-worker pattern correlation (single-worker scope first)

## Prototype
- Skipped — shipped; the running pipeline is the proof.
