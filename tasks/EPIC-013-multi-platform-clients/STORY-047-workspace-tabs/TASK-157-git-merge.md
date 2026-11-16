---
id: TASK-157
parent: STORY-047
feature: FEATURE-026
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-156]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Git tab: merge a feature branch

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

The pipeline ends in a merge (D20). The `git_merge` Dragon tool merges a feature branch with conflict detection and
then deletes it. This task adds `POST /api/v1/projects/{id}/git/branches/{name}/merge` (manage-projects
permission) over the same service: success merges and deletes the branch, a conflict answers 409 with the conflicting
files and leaves the repository as it was. The Git tab gets a Merge button with a confirmation step in the page (not a
browser dialog).

Decisions: FEATURE-026 D20.

## Acceptance criteria

- [ ] The endpoint merges and deletes the branch; on conflict it answers 409 listing the files and changes nothing
- [ ] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [ ] Merge asks for confirmation in the page, then shows the result; a conflict lists the files
- [ ] The merged branch leaves the branch list and its ready-to-merge item leaves Needs you
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Resolving conflicts in the browser

## Human test plan

- [ ] Merge a clean feature branch from the tab: `git log main` shows it and the branch is gone
- [ ] Make a conflicting change on main and merge: the conflict is shown and the repository is untouched

## Implementation plan

_Populated by `/tasks plan TASK-157` — leave empty until then._
