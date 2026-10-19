---
id: TASK-108
parent: STORY-038
feature: FEATURE-019
status: done
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
blocked: null
pr: null
github-issue: null
jira-key: null
---

# GitHub sign-in sessions are lost on every server restart — move refresh onto the Birko OAuth server

## Context

Found 2026-10-07 while closing TASK-036. GitHub sign-in (TASK-033) issues a refresh token kept by DraCode's own
`RefreshTokenStore` — a `ConcurrentDictionary` — and renewed through `/auth/refresh`. A server restart forgets every
refresh token, so every signed-in person has to sign in again once their access token expires. The Birko OAuth server
(`Birko.Security.OAuth.Server`, SQLite-backed stores since TASK-031) already persists its own refresh tokens.

## Acceptance criteria

> **Decided 2026-10-07 (user, at filing):** move onto the Birko OAuth server rather than persist DraCode's own store — its
> `refresh_token` grant already hashes, rotates, detects reuse and persists. The missing piece is a public "issue a token
> pair for a federated subject" call: Birko TASK-517 (`Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`).
> Moved here from STORY-017 on 2026-10-07.

- [x] GitHub sign-in issues its tokens through the Birko OAuth server (Birko TASK-517); `/auth/refresh` / `/auth/logout` use the server's refresh handling (or are replaced by `POST /token` `grant_type=refresh_token` with the web client updated)
- [x] `RefreshTokenStore` is deleted
- [x] A refresh token issued before a restart still renews after it — covered by a test
- [x] Rotation (a used refresh token is revoked) and expiry still hold

## Out of scope

- The GitHub sign-in flow itself (TASK-033) and the web login UI (TASK-056) — TASK-056 must use whichever refresh route this task leaves

## Human test plan

N/A — restart survival and rotation are testable against the store.

## Implementation plan

Done 2026-10-19.

1. `Auth/FederatedSessions.cs`: starts, renews and ends sign-in sessions on the Birko OAuth server.
   - `IssueAsync` calls `Token.IssueForSubjectAsync` (Birko TASK-517) for the subject `github:{id}`. The OAuth scope
     is the role permissions, space-separated. `name`, `roles` and `email` go in as extra claims, which the server
     keeps on the refresh record and re-issues on every refresh.
   - `RefreshAsync` uses the server's `refresh_token` grant: rotation, family revoke on reuse, expiry.
   - `RevokeAsync` (logout) revokes the one record. Birko has no public single-token revoke, so DraCode hashes the
     token the way the server does (hex SHA-256), pinned by a test; Birko TASK-678 filed for the public call.
   - Sessions belong to the public client `koboldlair-sign-in` (only the `refresh_token` grant, the six permissions
     as allowed scopes), created on first use.
   - A role set that grants no permission is refused: the server would widen an empty scope to everything the client
     allows.
2. `GitHubFederationService` issues through it; `/auth/refresh` and `/auth/logout` keep their routes and response shape
   (no web client calls them yet; TASK-056 should use them). `RefreshTokenStore` deleted.
3. Found on the way, fixed in its own commit: `AddBirkoSecurity` builds its token provider and bearer key from the
   configuration read before the host is built, which a `WebApplicationFactory`'s test configuration has not reached.
   Every test host signed and validated with the public development secret, while the OAuth server signed with the
   configured one. The token provider and the bearer key now resolve the secret from the runtime options.
4. Tests: `FederatedSessionsTests` (6) — a token issued before a restart renews after it (same SQLite file, new server;
   red with stores that don't persist), rotation and reuse revoking the family, expiry, logout, the hash format, and no
   permission refused. `JwtMiddlewareTests` refresh test now goes through the server and covers logout (red without the
   bearer-key fix). `GitHubFederationServiceTests` build the service on the OAuth stores.

## Close notes

- Closed 2026-10-19. Suite 273/273.
- Behaviour changes:
  - Lifetimes come from `Authentication:OAuth` (access 3600 s, refresh 14 days by default) instead of
    `Authentication:Jwt:ExpirationMinutes` / `RefreshExpirationDays`.
  - A session keeps the permissions it started with; a change to the role map applies at the next sign-in.
  - Presenting a used or logged-out refresh token signs that person out on every device (Birko's family revoke).
- With the JsonFile backend the OAuth stores are in-memory, so sessions still end at a restart there; SQLite is the
  default.
