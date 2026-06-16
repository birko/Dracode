---
id: FEATURE-060
created: 2026-02-03
owner: human
status: done
---

# Dragon Council sub-agents

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

The user-facing requirements assistant had grown to do everything at once — gathering specs, importing existing projects, handling git, and managing configuration. Cramming all of those responsibilities into one place made its behaviour muddy and harder to reason about, and harder to improve any single area without risking the others.

## Proposed shape

The assistant's duties are now split across a small council of focused specialists, each owning one area: one handles specifications, features, and approvals; one handles importing existing projects; one handles git; and one handles configuration, limits, and external paths. The user still talks to a single assistant, but behind the scenes the right specialist takes each request — giving cleaner separation of concerns and more predictable behaviour.

## Out of scope (initial)

- Changing the user-facing chat experience — the split is internal.
- Adding new capabilities beyond what already existed — this reorganises existing responsibilities.

## Prototype

- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
