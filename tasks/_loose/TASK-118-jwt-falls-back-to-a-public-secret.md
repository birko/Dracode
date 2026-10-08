---
id: TASK-118
parent: null
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# With no JWT secret configured, the server signs tokens with a secret published in the source — in every environment

## Context

`DraCode.KoboldLair.Server/Program.cs` falls back to `"KoboldLair-Development-Secret-Key-Do-Not-Use-In-Production!"` when
`ResolveSecret()` is empty, in **three** places: the `JwtTokenProvider` registration (~L61), `AddBirkoSecurity` (~L101)
and the OAuth server settings (~L142). The comment says development-only, but nothing checks the environment. A
production deploy that forgets the secret starts normally, and anyone who has read the repo can mint tokens. It also
defeats Birko's `JwtTokenProvider.EnsureSecretPresent`, which would otherwise fail at startup.

Found in the 2026-10-08 KoboldLair review (framework EPIC-019 / STORY-060–061).

## Acceptance criteria

- [ ] One place resolves the secret; the three call sites use it
- [ ] Outside Development, a missing secret stops startup with a message naming the config key
- [ ] In Development, either a per-machine generated secret or the fallback with a startup warning — decide and document
- [ ] Test: a Production host with no secret fails to start

## Human test plan

Start the server with `ASPNETCORE_ENVIRONMENT=Production` and no secret configured; it refuses to start.

## Implementation plan
