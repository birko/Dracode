---
id: TASK-161
parent: STORY-049
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-145]
blocks: [TASK-162, TASK-163]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Settings: providers

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

The current settings view (`b-tabs`, General and Providers) uses `/wyvern` provider commands. Rebuild it at
`#/settings` on `/api/v1/providers` (admin CRUD, mapped only when a provider master key is set) with the Shell CRUD
and form-modal pages. A non-admin does not see the Providers tab; when the endpoints are not mapped, the tab says how
to enable them. What the General tab holds today (client settings) moves into the new screen. The old view goes.

Decisions: FEATURE-026 D20.

## Acceptance criteria

- [ ] An admin lists, adds, edits, validates and removes providers through `/api/v1/providers`
- [ ] A non-admin sees no Providers tab; with the endpoints unmapped the tab explains why
- [ ] The old settings view and the legacy `styles.css` it relied on are removed
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- API keys (TASK-162)
- Budgets (TASK-163)
- Settings on phones (D21)

## Human test plan

- [ ] Add a provider with a wrong key: validation fails with a readable message; fix it: it validates and saves

## Implementation plan

_Populated by `/tasks plan TASK-161` — leave empty until then._
