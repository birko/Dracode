---
id: TASK-110
parent: STORY-038
feature: FEATURE-019
status: todo
priority: P3
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-519, TASK-520 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use the framework OAuth endpoint mapping and client-credentials hook; delete OAuthEndpoints and ServiceAccountTokenIssuer

## Context

`DraCode.KoboldLair.Server/Auth/OAuthEndpoints.cs` (267 lines) maps the Birko OAuth handlers to routes by hand, and
`ServiceAccountTokenIssuer.cs` (97) replaces the `client_credentials` grant only to shape the token. `Program.cs` also notes
that a space-delimited `scope` is read as one permission. Birko TASK-519 / TASK-520 cover all three.

Framework side: Birko TASK-519, TASK-520 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] The OAuth routes come from the framework mapping; service-account tokens keep `sub = service:<name>` and their permission scope through the framework hook
- [ ] `OAuthEndpoints.cs` and `ServiceAccountTokenIssuer.cs` are deleted (or reduced to the DraCode hook)
- [ ] The space-delimited-scope note in `Program.cs` is removed once the framework reads both forms
- [ ] `ServiceAccountTests`, OAuth server tests and `JwtMiddlewareTests` stay green

## Out of scope

- The framework change itself (Birko TASK-519, TASK-520)

## Human test plan

N/A — covered by the existing auth tests.

## Implementation plan
