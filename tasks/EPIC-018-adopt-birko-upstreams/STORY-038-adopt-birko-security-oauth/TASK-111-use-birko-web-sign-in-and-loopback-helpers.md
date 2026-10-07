---
id: TASK-111
parent: STORY-038
feature: FEATURE-019
status: todo
priority: P3
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-521, TASK-522 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use the framework GitHub web sign-in helpers and loopback bypass; delete the local copies

## Context

GitHub web sign-in uses DraCode's own `OAuthStateStore` (51 lines), `GitHubTokenExchanger` (57) and `GitHubUserInfoClient` (62),
and daemon mode its own `DaemonLoopback` (65). Birko TASK-521 / TASK-522 move them into the framework.

Framework side: Birko TASK-521, TASK-522 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] `GitHubAuthEndpoints` / `GitHubFederationService` use the framework state store, exchanger and user-info client; the allowlist and user provisioning stay in DraCode
- [ ] Daemon loopback bypass uses the framework option; `DaemonLoopback.cs` is deleted
- [ ] `GitHubFederationTests`, `DragonWebSocketAuthTests` and the loopback tests in `JwtMiddlewareTests` stay green

## Out of scope

- The framework change itself (Birko TASK-521, TASK-522)

## Human test plan

N/A — covered by the existing auth tests.

## Implementation plan
