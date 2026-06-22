---
id: TASK-037
parent: STORY-015
feature: FEATURE-017
status: done
priority: P1
assignee: ai
created: 2026-06-11
depends-on: []
blocks: [TASK-038, TASK-044, TASK-045]
pr: null
github-issue: null
jira-key: null
---

# Internal per-run event source (Kobold tool-loop event sink)

## Context

⭐ **Shared foundation for STORY-015 (WS) and STORY-016 (SSE)** — build once, two transports subscribe. Kobold today runs **headless**: `StartWorkingWithPlanAsync` / `StartWorkingWithPlanEnhancedAsync` return a `List<Message>` only at completion; the sole progress callback is `OnEscalation`. There is no per-tool-call / per-reflection / per-step hook. This task adds an internal per-run event stream that the tool loop publishes to (the `reflect` and `update_plan_step` tools already produce the structured data).

## Acceptance criteria

- [x] A per-run event abstraction keyed by `runId`, with subscribe/publish — `KoboldRunEventSource` (dedicated, not Birko.EventBus; see D-note in plan)
- [x] Kobold tool loop publishes: tool-call (start/result), reflection, plan-step update, completion, error — tool-call from the enhanced loop, reflection/plan-step from the tools (both paths), completion/error from the `finally`
- [x] Publication is **non-blocking** — bounded `Channel` + `DropOldest` + synchronous `TryWrite`; empty subscriber list = no-op; tested (`Slow_subscriber_drops_oldest`, `Publish_with_no_subscriber_is_a_noop`)
- [x] Subscribers can attach mid-run and receive subsequent events — tested (`Subscriber_attached_mid_run_only_sees_subsequent_events`)
- [x] Tests: a fake subscriber receives the expected event sequence — `KoboldRunEventSourceTests` (6 tests incl. scripted sequence); full suite 87/87 green

## Out of scope

- The `/kobold` WS transport (TASK-038) and SSE transport (TASK-045) — they consume this
- Persisting events (in-memory stream is sufficient for v1)

## Human test plan

- [ ] N/A — fully covered by automated tests (transports get their own manual tests)

## Implementation plan

> ⚠ **Test-scope note:** AC#5's "scripted Kobold run" — there is no fake `ILlmProvider` in the test
> project, so driving a real LLM-backed `Kobold.StartWorkingWith*Async` in a unit test is impractical.
> Realistic scope (not a criterion change): (a) direct source unit tests (publish/subscribe ordering,
> drop-if-no-subscriber, attach-mid-run, bounded DropOldest, CompleteRun) + (b) a scripted
> publish-point test that fires the publish helpers in run order with canned `ReflectionEntry`/`StepStatus`
> payloads. Swap in a full LLM-driven run later if a fake provider lands.

### Decisions
1. **Dedicated `KoboldRunEventSource`, NOT Birko.EventBus.** `InProcessEventBus.PublishAsync` is awaited +
   sequential (`MaxConcurrency<=1`) + keyed by event Type only — a slow WS/SSE handler would block the
   Kobold hot path, and there's no drop/attach-mid-run semantics. Build a per-`runId` source where each
   **subscriber** owns a `System.Threading.Channels.Channel` created **bounded + `BoundedChannelFullMode.DropOldest`**.
   `Publish` = synchronous `TryWrite` to every subscriber (never awaits/throws). Satisfies non-blocking +
   drop-if-no-subscriber (empty list = no-op) + attach-mid-run (new channel sees only later events; no replay,
   matching "no persistence"). Birko.EventBus stays for the existing cross-cutting domain events.
2. **`runId` = fresh `Guid` generated at each `Working` transition** (not ctor — a Kobold re-runs after
   resume; parallel mode runs multiple Kobolds per task). Add `Kobold.RunId`; event payload also carries
   `KoboldId`/`ProjectId`/`TaskId`/`AgentType` for routing. Transport (TASK-038) maps its connection to the
   active run via `KoboldFactory.GetKobold(...).RunId`.
3. **Optional/nullable** — `Kobold.SetRunEventSource(KoboldRunEventSource?)` (mirrors `SetSharedPlanningContext`);
   default null ⇒ every publish helper short-circuits. Headless `DrakeExecutionService` runs unaffected
   (null-check + empty-list iteration only).

### Event model (reuse existing types — `Events/Run/KoboldRunEvent.cs`, new)
`abstract record KoboldRunEvent { Guid RunId; Guid KoboldId; string? ProjectId; string? TaskId; string AgentType; DateTime OccurredAt; int Sequence; }`
(plain record, NOT `: EventBase` — no EventBus coupling). Derived:
- `ToolCallStartedEvent(string ToolName, string InputJson)` · `ToolCallResultEvent(string ToolName, string ResultPreview)`
- `ReflectionEvent(ReflectionEntry Entry, EscalationAlert? Escalation)` — reuse existing models verbatim
- `PlanStepUpdatedEvent(int StepIndex, StepStatus Status, string? Output, int Completed, int Total)` — reuse `StepStatus`
- `RunCompletedEvent(KoboldStatus FinalStatus, int CompletedSteps, int TotalSteps)` · `RunErrorEvent(string Message)`

### Publish points (grounded in Kobold.cs)
- `Agent.MessageCallback` shim fires `tool_call`(1340)/`tool_result`(1348)/`error`(1492) on **both** paths
  (basic loop lives in Birko.AI, hidden; the string callback is the universal hook) — publish `ToolCallStarted/Result`/`RunError` there.
- Enhanced path `RunWithStepDetectionAsync` (1170) — structured tool-call events at the visible `tool.ExecuteAsync` (1344).
- `ReflectionTool.ExecuteAsync` (after `Reflections.Add`, ~134 / after escalation ~218) → `ReflectionEvent`.
- `UpdatePlanStepTool.ExecuteAsync` (after status switch ~210) → `PlanStepUpdatedEvent`.
- Completion/error blocks of both `StartWorkingWithPlanAsync`/`...EnhancedAsync` → `RunCompleted`/`RunError`;
  `_runEventSource?.CompleteRun(RunId)` in the `finally` (lifetime cleanup — avoids channel leaks).
- Tools get `source`+`runId` via their existing static `RegisterContext(...)` (extend signature).

### Ordered steps
1. `Events/Run/KoboldRunEvent.cs` — base + 6 derived records.
2. `Services/KoboldRunEventSource.cs` — `ConcurrentDictionary<Guid, RunChannels>`; `Publish` (sync TryWrite, per-run `Interlocked` sequence), `Subscribe(runId)→IDisposable + ChannelReader`, `CompleteRun(runId)` (complete+remove). `try/catch` swallow around writes.
3. `Kobold.cs` — `RunId` prop + `SetRunEventSource` + `_runEventSource` + null-guarded `PublishRunEvent`; set `RunId` at `Working`; emit events at the points above; `CompleteRun` in `finally`; pass `_runEventSource,RunId` to the two `RegisterContext` calls.
4. `ReflectionTool.cs` / `UpdatePlanStepTool.cs` — extend `RegisterContext` (+`ClearContext`), publish.
5. `Factories/KoboldFactory.cs` — nullable `KoboldRunEventSource?` ctor param (mirror `_costTracker`), `kobold.SetRunEventSource(...)` on create.
6. `Server/Program.cs` — `AddSingleton<KoboldRunEventSource>()` (~656) + ensure the factory resolves it.
7. `Tests/Events/KoboldRunEventSourceTests.cs` — source unit tests + scripted publish-point sequence (per ⚠).

### Risks
- **Hot path** — `Publish` allocation-light, lock only around the per-run subscriber list, never await, no logging on the write path; `DropOldest` bounds memory under a slow consumer.
- **Lifetime/leak** — every run MUST `CompleteRun(runId)` in `finally`; transport disconnect disposes its subscription handle.
- **Parallel mode** — multiple Kobolds/task with distinct `RunId` (keying by RunId is correct). The tools' static `RegisterContext` is a pre-existing process-global shared slot — not introduced here, but note for parallel tool context.
- **Basic vs enhanced asymmetry** — structured reflection/step events fire on both (from tools); tool-call events are structured on enhanced, string-derived on basic. Transports (TASK-038/045) must tolerate both.

### Critical files
- **New:** `DraCode.KoboldLair/Events/Run/KoboldRunEvent.cs`, `DraCode.KoboldLair/Services/KoboldRunEventSource.cs`, `DraCode.KoboldLair.Tests/Events/KoboldRunEventSourceTests.cs`
- `DraCode.KoboldLair/Models/Agents/Kobold.cs` · `Agents/Tools/ReflectionTool.cs` · `Agents/Tools/UpdatePlanStepTool.cs` · `Factories/KoboldFactory.cs` · `DraCode.KoboldLair.Server/Program.cs`
