---
id: TASK-157
parent: STORY-047
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-156]
blocks: []
findings: []
pr: 31
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

- [x] The endpoint merges and deletes the branch; on conflict it answers 409 listing the files and changes nothing
- [x] The new endpoint is owner-scoped through `ApiOwnership` (404, not 403, for another owner's data; admins see all); server tests include a `github:` subject and another owner's data
- [x] Merge asks for confirmation in the page, then shows the result; a conflict lists the files
- [x] The merged branch leaves the branch list and its ready-to-merge item leaves Needs you
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- Resolving conflicts in the browser

## Human test plan

- [x] Merge a clean feature branch from the tab: `git log main` shows it and the branch is gone
- [x] Make a conflicting change on main and merge: the conflict is shown and the repository is untouched

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Endpoint** `POST /api/v1/projects/{id}/git/branches/{name}/merge` (`Api/GitEndpoints.cs`, manage-projects
  permission; the name URL-encoded). The branch must be one of the repository's (`GitService.BranchExistsAsync`, now
  shared with the diff) and not main, else 404, as is another owner's project. The merge is first checked read-only
  with `git merge-tree` (TASK-156's `CheckMergeAsync`): a conflict answers 409 `{ error, message, conflicts }` before git
  touches the repository. A clean branch goes through `GitService.MergeBranchAsync` (Sentinel's `git_merge` path, a
  `--no-ff` merge commit), then `DeleteBranchAsync`; a merge that still fails is 409 too (its conflicts, or its error).
  On success the branch's unread `feature_branch_ready` notifications are marked read, so it leaves the bell and Needs
  you; the answer is `{ merged, branch, mergeCommit, deleted }`.
- **Git tab:** the chosen branch has a merge box — "Merge into main" (with a warning when git expects conflicts), then
  a confirmation in the page ("Merge … into main and delete the branch?" Merge / Cancel), never a browser dialog. A
  merge says what it did above the list, reloads the branches (the merged one is gone) and refreshes Needs you and the
  bell; a conflict lists the files and says nothing changed.
- **Tests:** .NET `GitEndpointsTests` +3 on real repositories (a clean merge lands on main with its merge commit, the
  branch is deleted and only its ready notification is read; a conflict is 409 with the files and HEAD, branches and
  an uncommitted file untouched; another owner's project, view-only scope 403, unknown branch and main 404, admin may).
  e2e `git.spec.ts` +3 (merge after confirmation with the branch leaving the list and Needs you refreshed, Cancel, a
  conflict listed; the three red on the old tab). Suites .NET 526/526, e2e 127/127.
- **Human test** (throwaway Testing server on a scratch copy; a clean branch adding NOTES.md made in `waver`'s
  repository, with a ready-to-merge notification for it): merging it from the tab after the in-page confirmation gave
  "Merged … into main (4f262114); the branch was deleted."; `git log main` showed the merge commit and "Add NOTES.md",
  the branch was gone from git and the tab, and its ready-to-merge item left Needs you. `waver`'s real feature branch
  (main had moved on) showed the warning, and merging it listed `workspace/waver.js` as conflicting with HEAD, status
  and branches unchanged. No browser dialog; the only console line is Birko's `ApiClient` logging the expected 409.
