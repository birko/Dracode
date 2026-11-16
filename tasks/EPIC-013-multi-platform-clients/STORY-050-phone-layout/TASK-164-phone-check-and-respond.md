---
id: TASK-164
parent: STORY-050
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-145, TASK-151, TASK-153, TASK-159]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Phone check-and-respond layout

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Below the ~900px breakpoint the app is for checking and responding (D21): projects and progress, a read-only board,
Dragon chat and decisions, notifications. A bottom tab bar switches between them, with Dragon as its own tab (D22).
Git, settings, API keys and reprioritising stay desktop: those controls are not shown on phones. On tablet widths the
desktop layout stays, with Dragon opening as an overlay instead of docking (the plan sets the width range).

Decisions: FEATURE-026 D21, FEATURE-026 D22.

## Acceptance criteria

- [ ] Below ~900px a bottom tab bar offers Projects, Board, Dragon and Notifications
- [ ] The board is read only on phones: no drag, no Retry; Git, Settings and API keys are not reachable
- [ ] Dragon's tab shows the chat and decision cards full screen
- [ ] On tablet widths Dragon opens as an overlay over the desktop layout
- [ ] An e2e spec at phone and tablet viewports covers it; the suite stays green

## Out of scope

- Phone push (TASK-144, deferred)

## Human test plan

- [ ] On a real phone against a local server: check a project's progress, answer Dragon, open a notification
- [ ] On a tablet: Dragon opens over the board and closes again

## Implementation plan

_Populated by `/tasks plan TASK-164` — leave empty until then._
