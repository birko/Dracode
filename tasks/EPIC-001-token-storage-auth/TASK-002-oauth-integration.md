---
id: TASK-002
parent: EPIC-001
status: todo
priority: P2
assignee: ai
created: 2026-05-28
depends-on: [TASK-001]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# OAuth integration (Google, GitHub)

## Context

Add OAuth login flows alongside the existing JWT auth. Both providers via `Birko.Communication.OAuth`. GitHub already has a pre-configured device-flow provider in `Birko.Communication.OAuth.Providers`.

## Acceptance criteria

- [ ] Google OAuth provider configured (web flow + PKCE for desktop)
- [ ] GitHub OAuth wired up (device-flow provider already exists in Birko)
- [ ] `/auth/oauth/{provider}/login` + `/auth/oauth/{provider}/callback` endpoints
- [ ] Tokens stored via the encrypted storage from TASK-001
- [ ] User auto-created on first successful login (configurable: open / invite-only)
- [ ] Role mapping: configurable provider role → KoboldLair role
- [ ] Tests cover both providers via mocked OAuth servers

## Out of scope

- SAML / SSO enterprise providers (separate epic if needed)
- Multi-account linking (one OAuth identity per user in v1)

## Implementation plan

_Populated by `/tasks plan TASK-002` — leave empty until then._
