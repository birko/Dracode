---
id: TASK-133
parent: STORY-040
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-535 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use Birko conversation checkpoints; fix restored turns reading back empty

## Context

`ConversationCheckpoint.cs` (55) and `KoboldPlanService` L621–770. Today restored messages' text reads as `""`, and the 50-message trim can split a tool_use from its tool_result.

Framework side: Birko TASK-535 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Save / restore via the framework
- [ ] A resumed Kobold sees its prior text and tool turns
- [ ] Local checkpoint code deleted

## Out of scope

- The framework change itself (Birko TASK-535)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
