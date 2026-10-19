---
id: TASK-118
parent: STORY-041
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
findings: [SEC-1]
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

- [x] One place resolves the secret; the three call sites use it
- [x] Outside Development, a missing secret stops startup with a message naming the config key
- [x] In Development, either a per-machine generated secret or the fallback with a startup warning — decide and document
  (decided: the fallback with a startup warning; a generated secret would have to be persisted or every restart logs the local session out)
- [x] Test: a Production host with no secret fails to start

## Human test plan

Start the server with `ASPNETCORE_ENVIRONMENT=Production`, `Authentication__Jwt__Enabled=true` and no secret configured; it refuses
to start. (With JWT off the fallback signs nothing — no token endpoint is mapped — so the check applies only when it is on.)

## Implementation plan

Done 2026-10-19.

1. `Program.cs`: one static `ResolveJwtSecret(JwtAuthenticationConfiguration, IHostEnvironment)` with the
   `DevelopmentJwtSecret` constant; the `JwtTokenProvider` registration, `AddBirkoSecurity` and the OAuth server use it.
   It throws when `Authentication:Jwt:Enabled` is on, the secret is empty and the environment is not Development.
2. Startup resolves it once where `jwtRuntimeEnabled` is read (post-build, so `WebApplicationFactory` config is seen), so
   the host fails at start, not at the first login; in Development it logs a warning that the public secret is in use.
3. Test: `JwtSecretStartupTests` — Production without a secret fails to start (red on the old code), Production with a
   secret and Development without one start.

## Close notes

- Closed 2026-10-19. Suite 256/256. Human test plan run: `dotnet run` with Production + JWT on + empty
  `KOBOLDLAIR_JWT_SECRET` exits with the `InvalidOperationException` naming `Authentication:Jwt:Secret`.
- Scope narrowed on purpose: the check applies only with JWT auth on. With it off, nothing signs with the fallback.
