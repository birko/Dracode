---
id: FEATURE-023
created: 2026-05-31
owner: human
status: idea
---

# Discord bot client

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Many developers spend their day inside Discord. Switching to a browser or terminal to chat with the planning agent, kick off a run, or check project status is a context switch they'd rather avoid. They want to do all of that from within a Discord channel.

## Proposed shape

A Discord bot that connects to a hosted KoboldLair service on behalf of the team. Through slash commands, a user can start an ad-hoc agent run (output streamed into a dedicated thread), open a planning conversation, list the projects the bot can see, and check service status. The bot normally acts under its own service identity, but a `/login` command lets an individual Discord user link their own KoboldLair account so their commands run — and are audited — under their real identity. Streaming output is batched to stay within Discord's rate limits, and long runs use threads so they don't hit Discord's interaction timeout.

## Out of scope (initial)

- Running against a local/ephemeral daemon — the bot targets a remote hosted service only
- General chat outside the defined slash commands

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
