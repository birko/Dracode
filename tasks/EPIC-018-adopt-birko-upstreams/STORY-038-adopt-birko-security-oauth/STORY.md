---
id: STORY-038
parent: EPIC-018
status: planned
created: 2026-10-07
---

# Adopt the Birko security / OAuth upstreams

## User story

As the KoboldLair server, I want sign-in, OAuth stores and token handling to come from `Birko.Security*` /
`Birko.Communication.OAuth`, so that DraCode keeps only its own policy (role → permission map, GitHub allowlist, user
provisioning) and inherits framework fixes instead of maintaining copies.

## Source

The 2026-10-07 review of `DraCode.KoboldLair.Server/Auth/` (about 1,500 lines, roughly two thirds generic). Framework side:
Birko STORY-058 (TASK-517 – TASK-522).
