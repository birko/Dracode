---
id: FEATURE-073
created: 2026-01-11
owner: human
status: done
---

# Web client modernization

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
The web client leaned on a heavyweight UI framework and loosely-typed scripts, which made the interface slower to load, harder to maintain, and prone to subtle display bugs. We wanted a leaner, more reliable front end that we fully control.

## Proposed shape
Strip out the third-party UI framework entirely and rebuild the client on modern web standards: fully typed code organized into clear modules, and a hand-built layout system that adapts to any screen size. The result is a faster, dependency-free client that is easier to evolve.

## Out of scope (initial)
- Adding new user-facing features beyond the visual and structural rebuild
- Changing the server or API contracts

## Prototype
Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
