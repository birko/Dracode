---
id: TASK-106
parent: null
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
findings: [FIELD-020]
pr: null
github-issue: null
jira-key: null
---

# A run without the enhanced plan path publishes no tool-call events — `/kobold` ad-hoc clients see no progress

## Context

Found 2026-10-07 during TASK-083. `Kobold` publishes `ToolCallStartedEvent` / `ToolCallResultEvent` only inside
`RunWithStepDetectionAsync`, which `StartWorkingWithPlanEnhancedAsync` uses. `StartWorkingWithPlanAsync` — taken by every
run without a plan (all ad-hoc `/kobold` runs) and by planned runs when `Planning:UseEnhancedExecution` is off — calls
`Agent.RunAsync` directly, so between `RunStartedEvent` (TASK-083) and `RunCompletedEvent` nothing is published. A
`/kobold` ad-hoc client, the run registry and the coming SSE stream (TASK-045) see no tool calls for those runs; the
TASK-095 live check saw 30 `kobold_tool_call` frames only because that run had a plan.

## Acceptance criteria

- [x] A run through `StartWorkingWithPlanAsync` publishes `ToolCallStartedEvent` / `ToolCallResultEvent` for each tool
      call, like the enhanced path — while `Agent.RunAsync` runs, the Kobold wraps each agent tool in `RunEventPublishingTool` (publishes started/result) and restores the originals afterwards
- [x] Test: a plan-less Kobold that makes a tool call publishes started + result events in order before completion
      (fails without the fix) — `PlanlessRunEventsTests` (without the wrapping: only `run_started`, `run_completed`)

## Out of scope

- The SSE endpoint (TASK-045)
- Reflection events for plan-less runs (the reflect tool needs a plan)

## Human test plan

N/A — the event sequence is asserted by a test with a scripted provider.

## Implementation plan

Done 2026-10-07.

1. `RunEventPublishingTool`: a `Tool` decorator that publishes `ToolCallStartedEvent` / `ToolCallResultEvent` around the inner call.
2. `Kobold.StartWorkingWithPlanAsync`: wrap the agent's tools (only when the run has an event source) around `Agent.RunAsync`,
   unwrap in `finally`. A wrapper is needed because Birko's `Agent.RunAsync` reports tool calls only as text messages through
   the single message callback that Drake already uses.
3. Test: `PlanlessRunEventsTests`.

## Close notes

- Closed 2026-10-07. Suite 246/246. A structured tool-call hook on Birko's `Agent` would make the wrapper unnecessary; not
  needed while the decorator works.
