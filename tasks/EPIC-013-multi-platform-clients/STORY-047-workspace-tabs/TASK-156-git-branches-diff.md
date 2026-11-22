---
id: TASK-156
parent: STORY-047
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-150]
blocks: [TASK-157]
findings: []
pr: 30
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

- [x] The two endpoints return the branch list and a branch's diff
- [x] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [x] The Git tab lists branches; choosing one shows its changed files and the diff, file by file
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Merging (TASK-157)
- Git on phones (D21)

## Human test plan

- [x] In a local project with a finished feature branch, the tab shows it with the right ahead count and the diff matches `git diff main...<branch>`

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Read-only on purpose:** `GitService.CanMergeBranchAsync` (Sentinel's) tries a merge by checking out main in the
  project's folder — not something a GET may do while agents commit. The new `GitService.ListBranchesAsync` /
  `CheckMergeAsync` use `git merge-tree --write-tree` (git 2.38+), which touches neither the working tree nor the
  index; ahead/behind from `rev-list --left-right --count`. `GetBranchFilesAsync` gives `git diff main...branch` (what
  merging would bring) split per file, with status (`--name-status -M`), counts (`--numstat`) and the file's patch;
  patches are cut past 200 000 characters, and past 2 000 000 in all later files carry none. A branch name must be one
  of the repository's branches before it reaches git, so it can never be an option.
- **Endpoints** (`Api/GitEndpoints.cs`): `GET /api/v1/projects/{id}/git/branches` → `{ repository, baseBranch,
  branches }`; `GET /api/v1/projects/{id}/git/branches/{name}/diff` with the name URL-encoded (branch names hold
  slashes). Git runs in the project's git root — `ProjectService.ResolveProjectFolder`, now public: an imported
  project's `SourcePath`, else its folder. Another owner's project, or an unknown branch, is 404.
- **Git tab** (`views/git-tab.ts`, after Runs): the branches (name, feature, ahead/behind, "Merges cleanly" or
  "Conflicts in N files" with the files in its tooltip, last commit and age); choosing one shows its files (status,
  +/−, binary) linking to each file's diff, shown file by file with added, removed and hunk lines marked. Loaded when
  the tab opens and on Refresh, which keeps the chosen branch open — it does not poll, as each listing runs a merge
  check per branch.
- **Tests:** .NET `GitEndpointsTests` on real repositories (ahead/behind, clean and conflicting merges, and the working
  tree, current branch and an uncommitted file untouched; the per-file diff joined back equals `git diff main...`;
  another owner's project, an unknown branch and an option-like name 404; admin sees all). e2e `git.spec.ts`, 5 tests
  (all red on the old workspace). Suites .NET 523/523, e2e 124/124.
- **Human test** (throwaway Testing server on a scratch copy): `waver`'s finished `feature/6e5620f6-greet-function`
  showed 2 ahead / 6 behind as `git rev-list` counts them, a conflict in one file (main moved on), the same three
  files as `git diff --name-only main...`, and a diff identical, line for line, to `git diff main...<branch>` (350
  lines); HEAD, the current branch and `git status` unchanged afterwards. Light and dark at 1400px, 375px: no horizontal
  page scroll; no console errors.
