---
id: TASK-159
parent: STORY-048
feature: FEATURE-026
status: todo
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-145]
blocks: [TASK-164]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Notifications: header bell and project badges

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

`ProjectNotificationService` keeps each project's notifications and delivers them only over `/dragon`
(`project_notification`), to sessions whose current project matches. One stream, seen three ways (D17): this task adds
the REST side — `GET /api/v1/notifications` (the caller's projects, unread first) and
`POST /api/v1/notifications/{id}/read` — the header bell with an unread count and a list, and an unread badge on
the homepage's project cards. These are the same records Dragon receives, not a copy. How the bell hears of new ones
while open is the plan's choice.

Decisions: FEATURE-026 D17.

## Acceptance criteria

- [ ] The endpoints list and mark read the caller's notifications
- [ ] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [ ] The bell shows the unread count and a list; opening an item marks it read and opens its project
- [ ] Project cards show their unread count
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Decision cards in Dragon (TASK-160)
- Phone push (TASK-144, deferred)

## Human test plan

- [ ] Finish a feature in a local project: the bell count rises and the project's card shows a badge; opening the item clears both

## Implementation plan

_Populated by `/tasks plan TASK-159` — leave empty until then._
