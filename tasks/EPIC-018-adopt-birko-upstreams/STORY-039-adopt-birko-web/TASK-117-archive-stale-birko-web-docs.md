---
id: TASK-117
parent: STORY-039
feature: null
status: todo
priority: P3
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Archive the two stale Birko.Web docs (investigation report, shell fixes)

## Context

`docs/Birko.Web.Investigation-Report.md` (dated 2026-04-11) blames a blank screen on wrong esbuild aliases, `render()`
without error handling, a doubly created shell and a page that never mounts it. Only the last is still true (TASK-072): the
aliases are fixed in `build.js` and `BaseComponent` now wraps `render()`. `docs/Birko.Web.Shell-Fixes.md` (1,240 lines)
proposes framework changes that were mostly never made and that the 2026-10-07 review supersedes. Both still read as current.

## Acceptance criteria

- [ ] Both files move under an archive folder (or are deleted) with a one-line pointer to EPIC-018 / Birko EPIC-019
- [ ] `docs/README.md` and `CLAUDE.md` no longer present them as current

## Out of scope

- Writing new web docs

## Human test plan

N/A — documentation move.

## Implementation plan
