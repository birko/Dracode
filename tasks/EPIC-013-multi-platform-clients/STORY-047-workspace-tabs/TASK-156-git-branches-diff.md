---
id: TASK-156
parent: STORY-047
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-150]
blocks: [TASK-157]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Git tab: feature branches and their diff

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Git status and merge exist only as Dragon tools (`git_status`, `git_diff`, `git_merge` over `GitService`); there is
no REST and no branch list. This task adds `GET /api/v1/projects/{id}/git/branches` (feature branches with
ahead/behind main and whether they merge cleanly) and `GET .../git/branches/{name}/diff` (changed files and the
unified diff). For an imported project, git runs in its source path. The Git tab shows the branches, a branch's
changed files and its diff.

Decisions: FEATURE-026 D20.

## Acceptance criteria

- [ ] The two endpoints return the branch list and a branch's diff
- [ ] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [ ] The Git tab lists branches; choosing one shows its changed files and the diff, file by file
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Merging (TASK-157)
- Git on phones (D21)

## Human test plan

- [ ] In a local project with a finished feature branch, the tab shows it with the right ahead count and the diff matches `git diff main...<branch>`

## Implementation plan

_Populated by `/tasks plan TASK-156` — leave empty until then._
