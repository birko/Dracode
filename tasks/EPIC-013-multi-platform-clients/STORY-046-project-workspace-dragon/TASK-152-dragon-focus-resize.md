---
id: TASK-152
parent: STORY-046
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-151]
blocks: [TASK-160]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Dragon widens with focus

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

A thin chat is hard to read, so Dragon gets width only while in use (D13): it expands when focus enters it (a click,
or a keyboard shortcut) and shrinks when focus leaves, with no timers. Shrunk it is ~360px; expanded ~60% of the
width, pushing the centre aside. A "Keep expanded" pin holds it open and is remembered for the user across reloads
(where it is stored is the plan's choice). Below the ~900px breakpoint this does not apply: phones give Dragon its own
tab.

Decisions: FEATURE-026 D13.

## Acceptance criteria

- [ ] Focus entering the dock (click or the shortcut, shown in its tooltip) expands it to ~60%; focus leaving shrinks it to ~360px; nothing changes on a timer
- [ ] The transition takes ~150 ms and is instant under `prefers-reduced-motion`
- [ ] The pin keeps it expanded and is remembered for the user after a reload
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The board's compact list while Dragon is expanded (TASK-153)

## Human test plan

- [ ] Click into Dragon, type, click the board: it widens and narrows without jumping, and the typed text is kept
- [ ] The width change feels quick but not abrupt; with reduced motion set in the OS it is instant

## Implementation plan

_Populated by `/tasks plan TASK-152` — leave empty until then._
