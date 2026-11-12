---
id: TASK-141
parent: STORY-041
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-11-12
depends-on: []
blocks: []
blocked: null
pr: null
github-issue: null
jira-key: null
---

# The /api/v1 ownership rule reads the caller as `ICurrentUser.UserGuid`, so GitHub and service-account callers own nothing

## Context

Found while planning [[TASK-134]]. Token subjects are not all GUIDs:

- GitHub sign-in: `sub = "github:{id}"` (`Auth/GitHubFederationService.cs:40`)
- service accounts: `sub = "service:<name>"` (`Auth/ServiceAccountTokenIssuer.cs`)
- password login and the loopback bypass: a GUID

`ICurrentUser.UserGuid` parses the `NameIdentifier` claim as a GUID and returns null otherwise. The REST surface uses it
as the caller identity:

- `Api/ApiOwnership.cs:27` `CanAccess` — a non-admin with a non-GUID sub never matches any owner, so every project, task,
  plan and run is 404 to them, including ones they created through Dragon (Dragon stores the raw sub as `OwnerId`)
- `Api/ResourceEndpoints.cs:42–44` — project listing filters by `UserGuid?.ToString() ?? ""`
- `Api/ResourceEndpoints.cs:58` — `POST /projects` stores a null owner for these callers
- `Api/RunsEndpoints.cs:39, 78, 103` — run ownership, same defect
- `Api/ApiV1Endpoints.cs:28` — `/whoami` reports `userId: null`

The WebSocket endpoints read the raw claim (TASK-134 keeps that), so the two surfaces disagree on who the caller is.

## Acceptance criteria

- [ ] One caller-identity helper reads the raw `NameIdentifier` claim; every `/api/v1` site above uses it
- [ ] A `github:` caller sees and can open the projects/runs it owns, and still gets 404 for others'
- [ ] `/whoami` reports the raw subject
- [ ] Regression tests with `github:` and `service:` subjects, red on the current code

## Out of scope

- Replacing `ApiOwnership` with the framework predicate ([[TASK-139]]) — if that lands first, carry the fix into it
- Rewriting owners already stored as null by `POST /projects` (check whether any exist first)

## Human test plan

N/A — covered by tests unless a GitHub sign-in is configured locally.

## Implementation plan
