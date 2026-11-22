---
id: TASK-159
parent: STORY-048
feature: FEATURE-026
status: done
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-145]
blocks: [TASK-164]
findings: []
pr: 18
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

- [x] The endpoints list and mark read the caller's notifications
- [x] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [x] The bell shows the unread count and a list; opening an item marks it read and opens its project
- [x] Project cards show their unread count
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Decision cards in Dragon (TASK-160)
- Phone push (TASK-144, deferred)

## Human test plan

- [x] Finish a feature in a local project: the bell count rises and the project's card shows a badge; opening the item clears both

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Server:** `GET /api/v1/notifications` and `POST /api/v1/notifications/{id}/read` (`Api/NotificationEndpoints.cs`)
  over `ProjectNotificationService`, the records Dragon gets. Owner-scoped like `/projects`: the caller's projects, all
  for an admin with `?scope=all`; another owner's notification is neither listed nor markable (404). Unread first, then
  newest.
- **Read no longer deletes.** `MarkAsRead` used to remove a notification, so a bell could not list what was read; it now
  stamps `ProjectNotification.ReadAt`, and each project keeps its newest 50 read ones (`KeptReadPerProject`).
  `GetPendingNotifications` (Dragon, Warden's tool) still means unread. An old file without `readAt` reads as unread.
- **Restart bug fixed:** `Notify` started the project's in-memory list empty instead of loading the file first, so the
  first notification after a restart saved over the unread ones on disk (seen in the scratch data: `waver2`'s file held
  only the latest escalation). Every access now loads the file first.
- **Client:** `stores/notification-store.ts` (replacing the old, unused escalation store) polls
  `GET /notifications?scope=all` every 15 s while something listens and the page is visible, and once more on return;
  `markRead` updates the list at once. `components/notification-bell.ts` sits in the header's notifications slot: the
  bell with the unread count (Birko's `--b-text-on-danger` on red), and Birko's `b-dropdown-menu` listing up to 20, unread
  first, with "Unread"/"Earlier" headings; every value is escaped (the menu's labels are HTML), and rows are capped in
  width so the menu stays on a phone screen. Opening one marks it read and opens its project. With nobody signed in the
  bell renders nothing and nothing is requested. The homepage's cards show "N new" from the same store.
- **Tests:** .NET `NotificationEndpointsTests` (owner with a `github:` subject, unread first, mark read keeps it as read
  and clears it for Dragon; another owner's neither listed nor markable, admin sees all; the restart bug; the read cap).
  The three that build against the old code were red there. e2e `notifications.spec.ts`, 8 tests (7 red on the old client;
  the signed-out one has no bell either way); `fixtures.ts` gives every spec an empty notification list. Suites 504/504
  and 69/69.
- **Human test** (throwaway Testing server on a scratch copy, paths repointed to it, JWT on, LAN model): the bell showed
  2 and each project card "1 new" (`waver`'s project-complete, `waver2`'s escalation). Answering `waver2`'s open
  decision over `/dragon` raised "Decision answered…": without a reload the bell went to 3 and `waver2`'s badge to
  "2 new". Opening it from the bell went to the project and both counts dropped back. After a server restart the same
  unread ones were still there. At 375px the open menu stayed on screen in light and dark, no horizontal scroll, no
  console errors. The test found the badge number drawn red on red (`--b-color-danger-text`) and the menu touching the
  screen edge; both fixed.
