---
id: TASK-067
parent: STORY-020
feature: FEATURE-023
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-035]
blocks: [TASK-068, TASK-069]
pr: null
github-issue: null
jira-key: null
---

# Discord bot scaffold + config + slash-command registration

## Context

New `DraCode.KoboldLair.Discord/` project using Discord.Net. Bot authenticates to a remote KoboldLair daemon as a **service account** (JWT in env/config, from TASK-035). Config: Discord token, server URL, service JWT, optional guild allowlist.

## Acceptance criteria

- [ ] `DraCode.KoboldLair.Discord/` project scaffolded (Discord.Net), solution placement decided
- [ ] `appsettings.json`: Discord bot token, KoboldLair server URL, service JWT; optional per-guild allowlist
- [ ] Slash commands registered (no-op handlers ok): `/do`, `/chat`, `/projects`, `/status`, `/login`
- [ ] Bot connects to Discord and to KoboldLair (validates the service JWT on startup)
- [ ] Allowlist enforced: non-allowlisted guilds get a refusal

## Out of scope

- Command behaviour/streaming (TASK-068); user linkage (TASK-069)

## Human test plan

- [ ] Invite the bot to a test guild → slash commands appear; from a non-allowlisted guild → commands refused; startup logs confirm KoboldLair connection

## Implementation plan

_Populated by `/tasks plan TASK-067` — leave empty until then._
