---
id: TASK-147
parent: STORY-045
feature: FEATURE-026
status: todo
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-145, TASK-146]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# What needs you: homepage list and header pill

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Nothing on the server gathers what is waiting on the user. This task adds `GET /api/v1/needs-you`: for the caller's
projects, failed tasks, blocked tasks and feature branches ready to merge (the pending feature-branch-ready
notifications of `ProjectNotificationService`). Each item has a `kind`, so the decisions EPIC-014 brings can join
later without changing the shape.

The homepage gets a "Needs you" list grouped by project; inside a project, the header shows a pill "N other projects
need you" that leads back to that list.

Decisions: FEATURE-026 D10.

## Acceptance criteria

- [ ] `GET /api/v1/needs-you` returns items `{ kind, projectId, projectName, title, createdAt }` for the caller's projects
- [ ] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [ ] The homepage lists them grouped by project, newest first; each item opens its project
- [ ] Inside a project, the header pill counts the other projects with items, and is hidden at zero
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Decisions as items (they arrive with TASK-160)
- Deep links into a specific tab

## Human test plan

- [ ] Fail a task in a local project: it shows under Needs you on the homepage, and inside another project the pill reads 1

## Implementation plan

_Populated by `/tasks plan TASK-147` — leave empty until then._
