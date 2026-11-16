---
id: TASK-155
parent: STORY-047
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-150]
blocks: [TASK-057]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Runs tab: the project's runs

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

No endpoint lists runs. `RunRegistry` keeps each run's owner, mode and folded status in memory, including runs started
from the CLI over `/kobold`, so they do not survive a restart (persisting them is not part of this). This task adds
`GET /api/v1/runs?projectId=` (newest first; the run record gains the project id if it lacks one) and the Runs tab
listing this project's runs; the caller's ad-hoc runs (no project) appear in the same list, marked ad-hoc (D7).

Decisions: FEATURE-026 D7, FEATURE-026 D20.

## Acceptance criteria

- [ ] `GET /api/v1/runs` lists the caller's runs, filtered by project when asked
- [ ] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [ ] The Runs tab lists mode, status, start time and step progress; ad-hoc runs are marked
- [ ] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The live run view (TASK-057)
- Keeping runs across a server restart

## Human test plan

- [ ] Start an ad-hoc run from the CLI: it shows in the Runs tab, marked ad-hoc, with its status

## Implementation plan

_Populated by `/tasks plan TASK-155` — leave empty until then._
