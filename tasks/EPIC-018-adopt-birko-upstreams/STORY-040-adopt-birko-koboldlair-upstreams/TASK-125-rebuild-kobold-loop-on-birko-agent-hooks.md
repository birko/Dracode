---
id: TASK-125
parent: STORY-040
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: null
pr: null
github-issue: null
jira-key: null
---

# Rebuild Kobold step detection on Birko Agent hooks; delete the loop fork and the tool-wrapping

## Context

**Unblocked:** Birko TASK-527 landed (2026-10-09): `Agent.Hooks` / `IAgentHooks`, `ResumeAsync`; `AgentOptions.CheckpointInterval` removed, which is what currently breaks the KoboldLair.Server build.

`Models/Agents/Kobold.cs` `RunWithStepDetectionAsync` (lines 1300–1675) is a copy of `Agent.RunAsync`; line 1443 reads `SystemPrompt` by reflection; `WrapToolsForRunEvents` / `UnwrapTools` + `RunEventPublishingTool` exist only to observe tool calls. The fork already drops the cancellation token (cf. `_loose/TASK-079`).

Framework side: Birko TASK-527 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [x] Step detection and the reflection reminder run as per-iteration hooks on Birko's `Agent`
- [x] Tool-call run events come from the tool-call hooks; `RunEventPublishingTool` and the wrap/unwrap code deleted
- [x] No reflection on `SystemPrompt`
- [x] Cancellation reaches provider and tools; planless-run events (TASK-106 tests) stay green

## Out of scope

- The framework change itself (Birko TASK-527)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan

Done 2026-11-05.

1. `Kobold.RunHooks` (new partial file `Models/Agents/Kobold.RunHooks.cs`), an `AgentHooks` set on `Agent.Hooks` for the
   run and cleared in `finally`:
   - `OnToolCallStartingAsync` / `OnToolCallFinishedAsync` publish `ToolCallStartedEvent` / `ToolCallResultEvent` (both
     paths). A tool that throws now still gets its result event (`Error: <message>`).
   - Step detection (enhanced path only): an `update_plan_step` call closes the step it was on and starts the next one
     (metrics, logging) as it finishes; the rest waits for the next `OnIterationStartingAsync`, when the batch's
     tool results are in the conversation — auto-advance of a step whose files validate, the conversation checkpoint,
     and `Stop("all plan steps complete")`. The reflection reminder is `AppendUserMessage` every `CheckpointInterval`
     iterations of one step. `CompleteAsync` runs that bookkeeping once more after a `MaxIterationsReached` run, whose
     last tool turn no iteration start picks up.
2. `RunWithStepDetectionAsync` replaced by `RunPlanStepsAsync`: `Agent.RunWithOutcomeAsync`, or
   `ContinueWithOutcomeAsync(restored checkpoint, resume message)` when resuming; the stale-checkpoint delete keys off
   `AgentRunOutcome.LlmFailed` / `NotConfigured`. No reflection on `SystemPrompt` — Agent sends its own.
3. `StartWorkingWithPlanAsync`: hooks instead of `WrapToolsForRunEvents` / `UnwrapTools`; `RunEventPublishingTool`
   deleted. The token is now passed to `Agent.RunAsync`, and the enhanced path's no-plan fallback passes it on (it
   dropped it).
4. Cancellation: both paths catch `OperationCanceledException` when their token is cancelled, save the plan (and, on the
   enhanced path, a checkpoint) and return; the Kobold stays Working — the fork's graceful-shutdown contract, now also
   for a cancel that lands mid model call or mid tool batch. The saved conversation is the prefix up to the last
   iteration start (`RunHooks.ConsistentConversation`), so it always ends in a user turn and resumes.
5. `InternalsVisibleTo("DraCode.KoboldLair.Tests")` on the library, so tests can set `Kobold.ImplementationPlan`.

## Close notes

- Closed 2026-11-05. Suite 279/279 (273 + 6 in `KoboldRunHooksTests`).
- Red on the old code: the three cancellation tests (token never reaches provider/tools; a cancel during a model call
  hangs until the test's 10 s timeout, planless and enhanced). The other three — stop without another model call once
  the last step is marked, auto-advance, reminder every 3rd step iteration — pass on old and new: they pin the fork's
  behaviour the hooks had to carry over. `PlanlessRunEventsTests` (TASK-106) green; its wrapper assertion now checks
  `agent.Hooks` is detached.
- Behaviour that changed with the move onto Agent's loop:
  - A planned enhanced run that hits an LLM error now ends Failed, like the basic path: Agent writes the error into the
    conversation, which `ExtractErrorFromMessages` reads. The fork returned without it, so the Kobold stayed Working.
  - Remaining tool calls in the batch that finishes the last step still execute (the fork returned mid-batch).
  - The fork saved its step checkpoint mid-batch (assistant tool_use without its tool_result); it is now saved after the
    batch, so it is resumable as is.
  - Agent's `OnLlmResponseReceived` now fires on the enhanced path (the fork bypassed it).
- Tool bodies still mostly ignore the token they now receive — that is TASK-079.
- Human test plan: N/A (per task).
