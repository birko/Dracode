---
id: TASK-052
parent: STORY-019
feature: FEATURE-022
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-051, TASK-038]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# CLI run verbs: do / run / merge

## Context

The core execution verbs over the `/kobold` endpoint (STORY-015). `do <prompt>` = ad-hoc Kobold against cwd (worktree-always); `run <projectId>/<taskId>` = project-scoped; `merge <runId>` = `git merge` an earlier `do` run's worktree back into cwd's branch. Streaming output (token-by-token + tool-call annotations); scripting-clean (no logo for `do`/`run`).

## Acceptance criteria

- [ ] `koboldlair do <prompt> [--agent <type>] [--server <url>]` runs ad-hoc Kobold, streams output, prints resulting `runId` + worktree
- [ ] `koboldlair run <projectId>/<taskId>` runs project-scoped, streams, commits to feature branch (server-side)
- [ ] `koboldlair merge <runId>` merges the worktree from a prior `do` back into the cwd branch
- [ ] No ASCII logo / decorative chrome on `do`/`run` (clean for piping)
- [ ] Tests: verb arg parsing; happy-path against a stubbed `/kobold`

## Out of scope

- chat/analyze (TASK-053); auth/keys/projects (TASK-054)

## Human test plan

- [ ] In a scratch dir: `koboldlair do "add a hello function"` → see streamed tool calls, get a runId; `koboldlair merge <runId>` → changes appear in the working tree

## Implementation plan

_Populated by `/tasks plan TASK-052` — leave empty until then._
