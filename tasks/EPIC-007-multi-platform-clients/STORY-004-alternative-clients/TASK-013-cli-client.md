---
id: TASK-013
parent: STORY-004
status: todo
priority: P2
assignee: ai
created: 2026-05-28
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# KoboldLair CLI Client

## Context

Standalone CLI tool (similar to GitHub Copilot CLI / Claude Code) using Spectre.Console for the TUI. Two modes: Dragon (interactive requirements gathering) and Wyvern (direct analysis without Drake supervision).

## Acceptance criteria

- [ ] `koboldlair` binary (single-file publish, cross-platform)
- [ ] ASCII art logo + persistent top header with real-time stats (active Kobolds, tasks, tokens, mode)
- [ ] Interactive mode selection on startup
- [ ] Dragon Mode: interactive requirements chat
- [ ] Wyvern Mode: takes user request, creates task breakdown, stores in `.koboldlair/` folder in cwd, user manually invokes Kobolds
- [ ] Commands: `koboldlair`, `koboldlair chat`, `koboldlair analyze "request"`, `koboldlair run-task <id>`
- [ ] Color-coded status indicators + progress bars
- [ ] Keyboard shortcuts documented
- [ ] Published as cross-platform binary (Windows/macOS/Linux)

## Out of scope

- Plugin system in the CLI (same as TASK-003 plugins should work)
- Mobile / web companion app

## Implementation plan

_Populated by `/tasks plan TASK-013` — leave empty until then._
