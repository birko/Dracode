---
id: TASK-014
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

# VS Code extension

## Context

VS Code extension that hosts the agent loop inside the editor. Developer-experience play — many coders never leave VS Code.

## Acceptance criteria

- [ ] VS Code extension project under `clients/vscode/`
- [ ] Webview panel for Dragon chat interaction
- [ ] Command palette commands: "DraCode: Start session", "DraCode: Ask Dragon", "DraCode: Run analysis"
- [ ] Inline code actions (right-click → "Refactor with DraCode")
- [ ] Backend connection to KoboldLair.Server via existing JWT auth
- [ ] Settings: server URL, default agent, default provider
- [ ] Published to VS Code marketplace + open VSX

## Out of scope

- JetBrains plugin (separate effort if/when demand appears)
- Other editors (Sublime, Vim, Emacs)

## Implementation plan

_Populated by `/tasks plan TASK-014` — leave empty until then._
