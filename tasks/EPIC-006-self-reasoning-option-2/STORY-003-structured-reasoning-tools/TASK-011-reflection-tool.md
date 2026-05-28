---
id: TASK-011
parent: STORY-003
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

# ReflectionTool — structured reasoning capture

## Context

Option 1 (prompt-based CHECKPOINT blocks in `Kobold.cs:1070-1082`) is shipped. Option 2 adds an explicit tool that forces structured fields and triggers Drake intervention based on the values.

## Acceptance criteria

- [ ] `ReflectionTool` registered in Kobold's tool catalogue
- [ ] Structured fields: `progress_percent` (0-100), `blockers` (string[]), `confidence` (0-100), `adjustment` (string)
- [ ] Drake intervention triggered if `confidence < 30`
- [ ] Auto-escalate to Drake if progress stalled across 3+ consecutive checkpoints (delta < 5%)
- [ ] ReflectionTool calls captured in plan execution log
- [ ] Composes with existing Option 1 prompt block — the tool is called whenever the CHECKPOINT injection fires
- [ ] Configurable via `AgentOptions.UseStructuredReflection` (boolean, default false initially)

## Out of scope

- LLM-side reasoning quality improvements (prompt engineering, separate)

## Implementation plan

_Populated by `/tasks plan TASK-011` — leave empty until then._
