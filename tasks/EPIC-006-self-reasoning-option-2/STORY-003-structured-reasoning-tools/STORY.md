---
id: STORY-003
parent: EPIC-006
status: planned
created: 2026-05-28
---

# Structured reasoning tools

## User story

As a developer running long-running Kobolds, I want explicit reasoning checkpoints captured as structured tool calls (not free-text in prompts) so I can analyze decision patterns + intervene when an agent is stuck.

## Behaviour

- ReflectionTool emits structured fields: progress_percent, blockers, confidence, adjustment
- Drake intervenes when confidence < 30% or progress stalled across 3+ checkpoints
- ReasoningMonitorService runs externally and flags repeated error patterns + stuck loops
- Both compose with the existing prompt-based CHECKPOINT protocol (Option 1)
