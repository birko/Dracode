---
id: TASK-125
parent: STORY-040
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-527 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Rebuild Kobold step detection on Birko Agent hooks; delete the loop fork and the tool-wrapping

## Context

`Models/Agents/Kobold.cs` `RunWithStepDetectionAsync` (lines 1300–1675) is a copy of `Agent.RunAsync`; line 1443 reads `SystemPrompt` by reflection; `WrapToolsForRunEvents` / `UnwrapTools` + `RunEventPublishingTool` exist only to observe tool calls. The fork already drops the cancellation token (cf. `_loose/TASK-079`).

Framework side: Birko TASK-527 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Step detection and the reflection reminder run as per-iteration hooks on Birko's `Agent`
- [ ] Tool-call run events come from the tool-call hooks; `RunEventPublishingTool` and the wrap/unwrap code deleted
- [ ] No reflection on `SystemPrompt`
- [ ] Cancellation reaches provider and tools; planless-run events (TASK-106 tests) stay green

## Out of scope

- The framework change itself (Birko TASK-527)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
