---
id: TASK-053
parent: STORY-019
feature: FEATURE-022
status: blocked
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-051]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# CLI interactive verbs: chat / analyze + streaming UX

## Context

The interactive surfaces. `chat` = interactive Dragon session (over `/dragon`); `analyze` = invoke Wyvern, print task breakdown, persist into `.koboldlair/` in cwd. Spectre.Console TUI: ASCII logo on `chat` startup, header (active Kobolds, current project, daemon, tokens spent), color status + progress bars, token-by-token streaming.

## Acceptance criteria

- [ ] `koboldlair chat [--project <name>] [--server <url>]` runs an interactive Dragon session with streaming responses
- [ ] `koboldlair analyze "<request>"` invokes Wyvern, prints the task breakdown, persists artifacts to `./.koboldlair/`
- [ ] Interactive header shows active Kobolds / project / daemon / tokens-spent, refreshed live
- [ ] ASCII logo shown for `chat` only (not `do`/`run`); color-coded status + progress bars
- [ ] Tests: streaming render harness; analyze persists expected files

## Out of scope

- Run verbs (TASK-052); auth/projects (TASK-054)

## Human test plan

- [ ] `koboldlair chat` → logo + header render, type a message, watch streamed Dragon reply with a live token counter
- [ ] `koboldlair analyze "build a todo API"` → prints task breakdown and writes `.koboldlair/` artifacts

## Implementation plan

_Populated by `/tasks plan TASK-053` — leave empty until then._
