---
id: STORY-020
parent: EPIC-013
status: planned
created: 2026-05-28
---

# Discord bot client

## User story

As a developer who lives in Discord, I want a bot that lets me chat with Dragon, kick off Kobold runs, and check project status — without leaving the channel. The bot uses a service-account JWT to talk to a remote KoboldLair daemon.

## Behaviour

- New project `DraCode.KoboldLair.Discord/` (solution placement decided during planning)
- Library choice: Discord.Net (mature C# Discord library)
- Slash commands:
  - `/do <prompt>` — ad-hoc Kobold run; bot creates a thread for the run output
  - `/chat` — start a Dragon session in the current channel (thread per session)
  - `/projects` — list projects visible to the bot's service account
  - `/status` — show daemon state + active agents
  - `/login` — OAuth-link a Discord user to a KoboldLair user (so commands run under their identity, not the bot's)
- Bot identifies as a service account against KoboldLair via JWT in environment variable
- Streams tool-call output incrementally by editing the bot's reply message (Discord message edit rate-limit aware)
- Persistent thread → Dragon session mapping in a local SQLite/Birko.Data store

## Configuration

- `appsettings.json` for the bot: Discord bot token, KoboldLair server URL, KoboldLair service JWT
- Optional: per-Discord-server allowlist (only specific guilds can use the bot)

## Risks

- Discord rate limits + token streaming: heavy streaming risks throttling. Batch chunks (e.g. flush every 500ms or 200 chars).
- Long-running runs vs Discord's 15-min interaction token timeout: use thread messages + initial deferred response.
- User identity: by default everything runs as the bot service account. The `/login` linkage to real KoboldLair users is important for audit; treat it as MVP-blocking.
