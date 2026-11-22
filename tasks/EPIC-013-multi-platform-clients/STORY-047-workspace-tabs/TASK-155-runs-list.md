---
id: TASK-155
parent: STORY-047
feature: FEATURE-026
status: done
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

- [x] `GET /api/v1/runs` lists the caller's runs, filtered by project when asked
- [x] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [x] The Runs tab lists mode, status, start time and step progress; ad-hoc runs are marked
- [x] Lays out below the ~900px breakpoint without horizontal scroll (D22)
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The live run view (TASK-057)
- Keeping runs across a server restart

## Human test plan

- [x] Start an ad-hoc run from the CLI: it shows in the Runs tab, marked ad-hoc, with its status

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Run record:** `RunRegistry.RunRecord` gains `ProjectId` and `TaskId` (a project-mode run's) and `Prompt` (an ad-hoc
  run's first line, at most 200 characters), set where both transports register a run (`POST /api/v1/runs` and
  `/kobold`, through `RunsEndpoints.ProjectOf`). `GET /api/v1/runs/{id}` returns them too.
- **Endpoint** `GET /api/v1/runs`: the caller's runs, newest first; every run for an admin with `?scope=all`.
  `?projectId=` keeps that project's runs (404 for a project the caller cannot see), and `&adhoc=true` adds the caller's
  ad-hoc runs, which belong to no project (D7). Runs live in memory, so a restart empties the list (out of scope).
- **Runs tab** (`views/runs-tab.ts`, after Spec in the workspace tabs): each run's subject — an ad-hoc run's prompt, a
  project run's task title (from the project's tasks) — with its status badge, an "Ad-hoc" badge or its mode, start
  time, step progress (a bar and "n of m steps"), and a failed run's reason. Polls every 5 s while visible. Only runs
  started over `/kobold` (the CLI) or `POST /api/v1/runs` are listed; the pipeline's own Kobolds are on the Board.
- **Tests:** .NET `RunsListTests` (fields and order with a `github:` owner, project filter with and without ad-hoc;
  another owner's not listed, their ad-hoc runs not added, their project 404, admin sees all). e2e `runs.spec.ts`, 5
  tests, all red on the old workspace. Suites .NET 516/516, e2e 112/112.
- **Human test** (throwaway Testing server on a scratch copy, JWT on, LAN model): on `waver2`'s Runs tab, an ad-hoc run
  started over `/kobold` (as the CLI does; no CLI client exists yet, TASK-050/051) in a scratch git repo showed with
  its prompt and "Ad-hoc", completed within seconds (its branch and worktree made in the repo). Light and dark at
  1400px and 375px, no horizontal scroll, no console errors. The run finished too fast to be seen as Running; a
  completed ad-hoc run now reads "no plan steps" rather than "no plan steps yet".
