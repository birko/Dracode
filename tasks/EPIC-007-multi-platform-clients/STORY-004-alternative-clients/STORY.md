---
id: STORY-004
parent: EPIC-007
status: planned
created: 2026-05-28
---

# Alternative client surfaces (CLI, VS Code, Python)

## User story

As a developer outside the Web UI, I want CLI / IDE / scripting clients so I can use DraCode in the workflow I already have.

## Behaviour

- CLI tool with Dragon + Wyvern modes, Spectre.Console TUI
- VS Code extension hosts the agent loop inside the editor
- Python SDK provides programmatic access for notebooks and scripts
- All three reuse the same KoboldLair.Server backend
