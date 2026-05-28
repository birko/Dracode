---
id: STORY-014
parent: EPIC-012
status: planned
created: 2026-05-28
---

# Retire DraCode.WebSocket and DraCode.Web

## User story

As a maintainer, I want the old single-agent stack removed so that there is one obvious backend (`KoboldLair.Server`) and one obvious web client (`KoboldLair.Client`), with no dead code that still compiles and confuses contributors.

## Behaviour

- Remove `DraCode.WebSocket/` and `DraCode.Web/` from the filesystem
- Remove project references from `DraCode.slnx`
- Remove references from `DraCode.AppHost/Program.cs` (Aspire orchestration)
- Remove any shared bits from `DraCode.ServiceDefaults` that only the old stack used
- Scrub README + docs of pointers to `/ws` endpoint, port 5000 single-agent server, "WebSocket Server" running instructions
- Update `CLAUDE.md` "Project Structure" table — drop the two rows, update the "9 projects" count
- Update build commands documented in `CLAUDE.md` (remove `dotnet run --project DraCode.WebSocket` and `dotnet run --project DraCode.Web`)

## Ordering

Can ship first within this epic — the `/kobold` endpoint replaces the WS use case, but until STORY-015 lands, the old stack is just unused. Mechanical deletion PR.

## Risks

- Hidden depends-on in tests or sample scripts. Grep for `DraCode.WebSocket` and `DraCode.Web` across the repo before deleting.
- Anyone using the bare `/ws` endpoint loses it. The `/kobold` endpoint (STORY-015) is the migration target — its message protocol can be a near-superset of the old `connect/send` flow.
