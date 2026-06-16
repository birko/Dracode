---
id: FEATURE-024
created: 2026-05-31
owner: human
status: idea
---

# VSCode extension client

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Developers who work in VSCode have to leave their editor to use DraCode — opening a browser or terminal and mentally mapping results back onto the files they're editing. They'd rather drive the planning agent and review agent changes right inside the editor, against the very files they have open.

## Proposed shape

A VSCode extension that adds a "KoboldLair" side panel with two views: a Chat panel for the planning conversation and a Runs panel showing active and recent agent runs. Commands let users start a chat, run an agent on the currently selected code, connect to a server, and sign in. When an agent run touches workspace files, the changes appear as a native VSCode diff so they can be reviewed inline before accepting. The extension finds a local background service automatically, or connects to a configured server, and signs in using VSCode's built-in GitHub authentication.

## Out of scope (initial)

- Choosing the final Marketplace publisher identity (placeholder for now; decided during planning)
- Confirming exact OAuth scopes (to be settled during planning)

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
