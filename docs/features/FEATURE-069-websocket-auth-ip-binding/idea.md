---
id: FEATURE-069
created: 2026-01-15
owner: human
status: done
---

# WebSocket authentication with IP binding

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
The WebSocket connections that drive the agent system were open by default. Anyone who could reach the server could connect, and there was no way to lock a connection down to a known client. If a connection token ever leaked, there was nothing stopping someone else from reusing it from a different machine.

## Proposed shape
Add optional token-based authentication for WebSocket connections, with an optional extra layer that binds a token to a specific client IP address. That way a stolen token can't simply be reused from somewhere else. Tokens are configured through environment variables, and the system can still detect the real client address even when traffic passes through a proxy. The whole feature stays off by default so existing setups keep working unchanged, and any failed attempt is written to the log for visibility.

## Out of scope (initial)
- Authentication enabled by default (kept opt-in for backward compatibility)
- User accounts, roles, or full identity management
- Token rotation or expiry workflows

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
