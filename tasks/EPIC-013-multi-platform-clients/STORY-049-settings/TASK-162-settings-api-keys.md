---
id: TASK-162
parent: STORY-049
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-161]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Settings: service-account API keys

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Service accounts are OAuth clients: created with `POST /register`, disabled with `POST /register/{clientId}/disable`,
used through the client-credentials token; the CLI's `koboldlair keys` does create and revoke. Nothing lists them —
the OAuth client store cannot query (`ReadAsync(predicate)` throws). This task adds an owner-scoped
`GET /api/v1/keys` (the caller's service accounts: id, name, created, disabled), recording the creator as owner if
registration does not already, and the Settings → API keys tab: create (the secret shown once, with copy), list,
revoke. This settles the old "list has no server support" question by building the list.

Decisions: FEATURE-026 D6.

## Acceptance criteria

- [ ] `GET /api/v1/keys` lists only the caller's service accounts
- [ ] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [ ] Creating a key shows its secret once with a copy button; it is never shown again
- [ ] Revoking a key disables it; a token request with it then fails
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Key scopes or expiry (not decided)

## Human test plan

- [ ] Create a key, use it from the CLI, revoke it in the browser: the CLI's next call is refused

## Implementation plan

_Populated by `/tasks plan TASK-162` — leave empty until then._
