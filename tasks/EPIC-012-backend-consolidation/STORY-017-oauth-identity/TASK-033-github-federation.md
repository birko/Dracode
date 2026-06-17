---
id: TASK-033
parent: STORY-017
feature: FEATURE-019
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-030]
blocks: [TASK-036]
pr: null
github-issue: null
jira-key: null
---

# GitHub federation for human login

## Context

Human login federates to GitHub via the existing `GitHubOAuthProvider` (`Birko.Communication.OAuth.Providers`). DraCode consumes the upstream GitHub identity, then mints its **own** tokens (TASK-030's server) for its clients. The stable GitHub `sub` maps to a DraCode `User` (TASK-034).

## Acceptance criteria

- [ ] `GitHubOAuthProvider` wired with client id/secret config (env vars, disabled-by-default)
- [ ] Web UI login redirects to GitHub OAuth; callback completes the flow and issues a DraCode token
- [ ] Upstream GitHub `sub` resolved → DraCode `User` (created on first login; reused after)
- [ ] CLI device-code flow (TASK-030) and GitHub web flow both terminate in a DraCode-issued token
- [ ] Tests cover the callback → user-resolution path (GitHub calls mocked)

## Out of scope

- The `User` entity schema + `ownerId` (TASK-034)
- Additional IdPs (Microsoft/Google) — config-extensible but not implemented here

## Human test plan

- [ ] Click "Login with GitHub" in the Web UI → complete GitHub consent → land back authenticated with a DraCode session
- [ ] Run `koboldlair login` (device flow) → approve in browser → CLI receives and caches a token

## Implementation plan

_Populated by `/tasks plan TASK-033` — leave empty until then._
