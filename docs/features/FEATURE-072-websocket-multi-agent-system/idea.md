---
id: FEATURE-072
created: 2026-01-10
owner: human
status: done
---

# WebSocket multi-agent system

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem
There was no shared, real-time way to talk to multiple AI agents at once. Each agent interaction stood alone, with no single connection that could host several agents, no central place to configure providers, and no orchestration to run the pieces together.

## Proposed shape
Build the foundational multi-agent system over a single WebSocket connection. One connection can host multiple agents, each identified by an agent ID, with its own independent conversation history. The server holds the provider configuration and expands secrets from environment variables. A simple command set (list, connect, disconnect, reset, send) drives the agents, and responses from different agents can be compared side by side. This work introduced the core projects: the WebSocket API server, the TypeScript web client, and the .NET Aspire orchestration with its monitoring dashboard.

## Out of scope (initial)
- The full Dragon/Wyrm/Wyvern/Drake/Kobold orchestration hierarchy (came later)
- Persistent server-side conversation storage
- Authentication (added separately in a later release)

## Prototype
- Skipped — shipped feature backfilled from CHANGELOG; the released build is the proof.
