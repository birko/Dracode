---
id: FEATURE-020
created: 2026-05-31
owner: human
status: idea
---

# Daemon mode for KoboldLair.Server

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Every command-line invocation pays the cost of starting the server from cold. For interactive commands like a chat or an analysis run, that startup delay is felt on every single call, which makes the CLI feel sluggish.

## Proposed shape

Let the server run quietly in the background as a per-user daemon. The first command starts it; every command after that connects instantly to the already-running background process. The daemon is discoverable through a small state file in the user's home folder, and it can be checked on or stopped cleanly with simple commands.

Clients automatically find and reuse a running daemon, or start one on demand if none is running. Safeguards prevent two daemons from fighting over the same user, handle leftover state from an unclean shutdown, and let an existing connection finish its work gracefully before the daemon stops.

## Out of scope (initial)

- A system-wide service shared by multiple users (this is per-user)
- Remote daemon management beyond pointing a client at a given address
- Authentication of remote daemons, which is handled by FEATURE-019

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
