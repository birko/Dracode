---
id: TASK-071
parent: EPIC-012
feature: FEATURE-077
status: done
priority: P1
assignee: ai
created: 2026-06-16
depends-on: []
blocks: [TASK-031]
pr: null
github-issue: null
jira-key: null
---

# Unify Birko framework source compilation into DraCode.Birko

## Context

The Birko framework is consumed via shared `.projitems` (source inclusion). Three projects
(`DraCode.Birko`, `DraCode.KoboldLair`, `DraCode.KoboldLair.Server`) plus `DraCode.KoboldLair.Tests`
each re-imported overlapping framework projitems, so identity-bearing types (`AbstractModel`,
`IAsyncStore<T>`, `ILoadable`, `AbstractConnector`, the OAuth models/stores) were compiled into
**multiple assemblies**. The build only worked because CS0436 "source-wins" papered over the
duplicates. This blocked TASK-031: a `: AsyncSQLiteStore<OAuthClient>` subclass wouldn't even compile
because `OAuthClient`'s `AbstractModel` (one assembly) ≠ `AsyncSQLiteStore`'s `AbstractModel` (another).

Discovered while picking up TASK-031. Fixed here as a prerequisite.

## Acceptance criteria

- [x] `DraCode.Birko` is the single source-compiler of the framework's identity-bearing types
      (Data.Core/Stores/SQL chain, Models, Security, Security.Jwt, Security.OAuth.Server,
      Serialization, Time, EventBus, Caching, MessageQueue, Validation, Data.EventSourcing)
- [x] `DraCode.KoboldLair` imports no Birko `.projitems` — references `DraCode.Birko` compiled
- [x] `DraCode.KoboldLair.Server` keeps only ASP.NET-coupled projitems (WebSocket/SSE/Communication/
      BackgroundJobs/MessageQueue.InMemory); all data/security/OAuth source dropped
- [x] `DraCode.KoboldLair.Tests` imports no framework projitems — consumes the compiled identity
- [x] Required `PackageReference`s (Microsoft.Data.Sqlite, System.IdentityModel.Tokens.Jwt,
      Microsoft.Extensions.Options) moved to `DraCode.Birko`
- [x] `dotnet build DraCode.slnx` → 0 errors; **CS0436 count 1172 → 0** (total warnings 616 → 28)
- [x] `dotnet test` → all 35 tests pass

## Out of scope

- The actual SQLite OAuth store implementation (TASK-031) — this only unblocks it at the build level
- `DraCode` CLI — already a clean DraCode.Birko consumer, no change needed

## Human test plan

- [ ] N/A — covered by build + automated tests (CS0436 = 0, 35 tests green)

## Implementation plan

Done. Expanded `DraCode.Birko.csproj` to import the full identity-bearing framework projitems set and
the packages those sources need; stripped the duplicate imports from `DraCode.KoboldLair.csproj`,
`DraCode.KoboldLair.Server.csproj`, and `DraCode.KoboldLair.Tests.csproj` so they consume the compiled
aggregator. Verified clean build (0 CS0436) and green test run.
