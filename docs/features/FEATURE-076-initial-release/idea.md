---
id: FEATURE-076
created: 2025-12-01
owner: human
status: done
---

# Initial release

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
There was no DraCode product yet. We needed a first usable release: a command-line assistant that could talk to multiple AI providers, take real actions on files and the system, and be configured safely.

## Proposed shape
Ship a polished command-line application that lets users pick from several major AI providers, gives the assistant a core set of practical actions (browsing and reading files, writing files, searching code, running commands, asking the user, and showing output), and presents it all through a clean interactive terminal experience. Include sign-in for one provider, safety boundaries on file access, flexible configuration, and a back-and-forth conversation loop with sensible limits.

## Out of scope (initial)
- Web or graphical interfaces (command line only)
- The multi-agent orchestration introduced in later releases

## Prototype
Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
