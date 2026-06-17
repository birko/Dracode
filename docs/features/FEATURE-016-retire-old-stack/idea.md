---
id: FEATURE-016
created: 2026-05-31
owner: human
status: done
---

# Retire DraCode.WebSocket and DraCode.Web

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

DraCode carries two parallel server/client stacks. The old one is a single-agent web server and its browser client that predate the current pipeline. It no longer connects to projects, specifications, or the multi-agent pipeline, yet it still builds and ships. That dead weight confuses contributors who can't tell which backend is the real one.

## Proposed shape

Remove the old single-agent server and its browser client entirely so there is one obvious backend and one obvious web client. This means deleting the old folders, dropping them from the solution and the orchestration setup, and scrubbing the documentation of any pointers to the retired endpoint and its run instructions.

This is a clean mechanical removal. It can land first in the consolidation effort because the new endpoint that replaces the old use case (see FEATURE-017) will take over before anyone misses the old one.

## Out of scope (initial)

- Building the replacement endpoint (that is FEATURE-017)
- Any behaviour change to the surviving backend or client
- Preserving the old shared-token connect/send protocol beyond what the replacement provides

## Prototype
- Skipped — pure mechanical removal, no user-facing surface to prototype.
