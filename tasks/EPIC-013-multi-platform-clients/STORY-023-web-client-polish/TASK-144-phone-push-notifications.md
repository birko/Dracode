---
id: TASK-144
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P3
assignee: ai
created: 2026-11-12
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Phone push notifications when a project needs you

## Context

FEATURE-026 D24 (deferred, owner 2026-11-12): when a decision arrives or a project fails while the web app is closed,
the phone gets a push notification. In v1 the in-app badge, header bell and Dragon decision card cover it (D17).

## Acceptance criteria

- [ ] The web client is installable as a PWA and asks for push permission
- [ ] The server sends web-push messages (VAPID) for the notification stream's "needs you" and "failing" events
- [ ] Tapping a notification opens the project (and Dragon, for a decision)
- [ ] Per-user opt-in / opt-out in settings

## Out of scope

- In-app notifications (part of the UI rebuild, D17)

## Human test plan

- [ ] Close the app on a phone, trigger a decision, receive the push, tap it and land on the decision

## Implementation plan
