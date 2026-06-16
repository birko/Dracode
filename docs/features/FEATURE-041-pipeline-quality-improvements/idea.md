---
id: FEATURE-041
created: 2026-03-15
owner: human
status: done
---

# Pipeline quality improvements

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
The automated build pipeline often produced code that did not match the spec: vague task descriptions, missing acceptance criteria, agents inventing wrong file locations or duplicating code, and no guarantee that every requirement was actually addressed. Errors surfaced late, after a lot of work was wasted.

## Proposed shape
Tighten every stage of the pipeline so quality is enforced up front. The early analysis stage now pins down concrete languages, the tech stack, hard constraints, and things explicitly out of scope. Task breakdowns must carry acceptance criteria, exact target files, and the public interfaces to build, with every spec requirement traced to at least one task. Workers receive the real export signatures of existing code, follow mandatory execution rules (read before write, no duplicate declarations, consistent imports), and see project constraints as a prominent reminder. After a task finishes, the system runs a compilation check to catch breakage immediately.

## Out of scope (initial)
- Runtime/integration testing beyond compilation checks
- Human code review automation
- Changes to the LLM providers themselves

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
