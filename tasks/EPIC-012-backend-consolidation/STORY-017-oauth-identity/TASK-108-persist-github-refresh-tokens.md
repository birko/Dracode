---
id: TASK-108
parent: STORY-017
feature: FEATURE-019
status: todo
priority: P3
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# GitHub sign-in sessions are lost on every server restart (in-memory refresh tokens)

## Context

Found 2026-10-07 while closing TASK-036. GitHub sign-in (TASK-033) issues a refresh token kept by DraCode's own
`RefreshTokenStore` — a `ConcurrentDictionary` — and renewed through `/auth/refresh`. A server restart forgets every
refresh token, so every signed-in person has to sign in again once their access token expires. The Birko OAuth server
(`Birko.Security.OAuth.Server`, SQLite-backed stores since TASK-031) already persists its own refresh tokens.

## Acceptance criteria

- [ ] Decide: persist `RefreshTokenStore` (e.g. a SQLite table like the OAuth stores) or issue GitHub sign-in tokens through
      the Birko OAuth server's refresh handling and retire `RefreshTokenStore`
- [ ] A refresh token issued before a restart still renews after it — covered by a test
- [ ] Rotation (a used refresh token is revoked) and expiry still hold

## Out of scope

- The GitHub sign-in flow itself (TASK-033) and the web login UI (TASK-056)

## Human test plan

N/A — restart survival and rotation are testable against the store.

## Implementation plan
