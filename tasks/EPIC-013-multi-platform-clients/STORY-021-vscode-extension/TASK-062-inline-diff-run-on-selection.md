---
id: TASK-062
parent: STORY-021
feature: FEATURE-024
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-060]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# VSCode inline diff view + Run on Selection

## Context

Editor-integrated affordances. When a Kobold run touches workspace files, present them in a VSCode diff editor (workspace vs worktree state). "Run on Selection": highlight code → invoke an ad-hoc Kobold with the selection as prompt context.

## Acceptance criteria

- [ ] Kobold run file changes shown as VSCode diff editors (current workspace vs worktree result)
- [ ] User can accept/apply or discard changes from the diff view
- [ ] `Run on Selection`: selected text becomes the ad-hoc Kobold prompt context; result returns as a diff
- [ ] Works against both local-daemon and remote-server targets
- [ ] Tests where supported

## Out of scope

- Panel wiring (TASK-060), auth (TASK-061)

## Human test plan

- [ ] Select a function, run `Run on Selection` with "add error handling" → a diff editor opens showing proposed changes; apply → file updates; discard → file unchanged

## Implementation plan

_Populated by `/tasks plan TASK-062` — leave empty until then._
