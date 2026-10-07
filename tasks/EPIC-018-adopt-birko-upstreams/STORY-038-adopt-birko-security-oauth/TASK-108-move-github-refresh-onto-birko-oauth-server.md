---
id: TASK-108
parent: STORY-038
feature: FEATURE-019
status: todo
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-517 (Birko.Framework)'
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

- [ ] GitHub sign-in issues its tokens through the Birko OAuth server (Birko TASK-517); `/auth/refresh` / `/auth/logout` use the server's refresh handling (or are replaced by `POST /token` `grant_type=refresh_token` with the web client updated)
- [ ] `RefreshTokenStore` is deleted
- [ ] A refresh token issued before a restart still renews after it — covered by a test
- [ ] Rotation (a used refresh token is revoked) and expiry still hold

## Out of scope

- The GitHub sign-in flow itself (TASK-033) and the web login UI (TASK-056) — TASK-056 must use whichever refresh route this task leaves

## Human test plan

N/A — restart survival and rotation are testable against the store.

## Implementation plan
