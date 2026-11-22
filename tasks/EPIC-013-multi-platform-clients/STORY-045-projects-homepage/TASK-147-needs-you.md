---
id: TASK-147
parent: STORY-045
feature: FEATURE-026
status: done
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

- [x] `GET /api/v1/needs-you` returns items `{ kind, projectId, projectName, title, createdAt }` for the caller's projects
- [x] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [x] The homepage lists them grouped by project, newest first; each item opens its project
- [x] Inside a project, the header pill counts the other projects with items, and is hidden at zero
- [x] Open decisions are items too (kind `decision`, TASK-160's cards: the `decision_required` notifications, or the
      `/dragon` cards), opening the project where the dock shows the card, and leave once answered
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Deep links into a specific tab

## Human test plan

- [x] Fail a task in a local project: it shows under Needs you on the homepage, and inside another project the pill reads 1

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Endpoint** `GET /api/v1/needs-you` (`Api/NeedsYouEndpoints.cs`): items `{ id, kind, projectId, projectName, title,
  createdAt }`, newest first, for the caller's projects (`ApiOwnership.VisibleProjects`, now shared with the
  notification endpoints; `?scope=all` for an admin). Kinds: `decision` (the decision store's open ones, the source
  TASK-160's cards read, so an answered one is gone), `failed_task` / `blocked_task` (the task store's Failed and
  BlockedByFailure; a task waiting on a decision is its decision), `merge_ready` (unread `feature_branch_ready`
  notifications). `?projectId=` narrows to one project; another owner's reads as 404 — the listing has no id to 404
  on otherwise.
- **Client:** `stores/needs-you-store.ts` polls like the bell's (15 s while shown and visible) and clears on sign-out.
  The homepage shows "Needs you" above the cards — grouped by project, the project with the newest item first, each
  item a badge (Decision, Failed, Blocked, Ready to merge), its title and age, opening the project — or "Nothing
  needs you right now." The header pill (`components/needs-you-pill.ts`, beside the picker) counts the other projects
  with items, links home, and is hidden at zero, outside a project and signed out; on a phone it is "⚑ N" with the
  words in its tooltip and label. Answering a decision card and a successful Retry refresh the list at once, and so
  does opening the homepage (the pill may already keep the store polling, so the list could otherwise be 15 s old —
  found by the human test).
- **Tests:** .NET `NeedsYouEndpointsTests` (kinds and order with a `github:` owner, an answered decision gone,
  `projectId`; another owner's not listed and 404, admin sees all). e2e `needs-you.spec.ts`, 7 tests (6 red on the
  old client; the fresh-list one red without its fix): the grouped list and opening an item, the all-clear line, the
  pill's count, link and zero, an answered decision leaving, a fresh list on the homepage, escaping, phone width with
  nothing drawn over the pill. Suites .NET 514/514, e2e 107/107.
- **Human test** (throwaway Testing server on a scratch copy, JWT on): the homepage listed waver2's real failed task
  and its open decision; inside waver the pill read "1 other project needs you", inside waver2 there was none; after
  Retry on waver2's Board the homepage listed only the decision (the first run showed the stale list, now fixed).
  Light and dark at 1400px, 375px: no horizontal scroll; the first phone run showed the pill's words cut off under the
  bell's count, now "⚑ 1". No console errors.
