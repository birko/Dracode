---
id: TASK-038
parent: STORY-015
feature: FEATURE-017
status: review
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-037]
blocks: [TASK-039, TASK-040]
pr: null
github-issue: null
jira-key: null
---

# /kobold WebSocket endpoint + message protocol

## Context

`app.MapWebSocket("/kobold", ...)` (Birko.Communication.WebSocket extension, same as `/dragon`). Parses the initial-message mode payload, subscribes the WS connection to TASK-037's run-event source, and emits the `kobold_*` message shapes. Mode handlers (ad-hoc/project) are TASK-039/040.

## Acceptance criteria

- [x] `/kobold` endpoint mapped; auth applied consistently with the other WS endpoints — `app.Map("/kobold", …)` in `Program.cs` mirrors `/dragon` (JWT-bearer 401 gate + `ResolveCaller`; loopback owner when JWT off). **Open question resolved:** JWT-bearer pipeline, no Birko WS middleware.
- [x] Initial payload parsed; dispatches to ad-hoc vs project handler by `mode` — `KoboldEndpointService.HandleWebSocketAsync` parses `KoboldRunRequest`, matches `IKoboldRunModeHandler.Mode` (unknown → `error` frame)
- [x] Emits `kobold_run_started` ({runId, worktree, mode}), then streams `kobold_stream`, `kobold_tool_call`, `kobold_reflect`, `kobold_complete`, `error` — `PumpAsync` + `KoboldWireMessage.From` translator; subscribes **before** `StartAsync` to avoid the drop-before-subscribe race
- [x] Client disconnect mid-run cleans up the subscription without killing the Kobold — `WatchForCloseAsync` cancels the pump CTS only (no `CompleteRun`); `using` disposes the subscription. Tested: post-cancel a late subscriber still receives events
- [x] Tests: protocol round-trip with a fake run-event producer — `KoboldEndpointProtocolTests` (3 tests) drive `PumpAsync` against a real `KoboldRunEventSource`; full suite 90/90 green

## Out of scope

- Ad-hoc git/worktree mechanics (TASK-039); project plan/worktree reuse (TASK-040)

## Human test plan

**Deferred — both mode handlers are stubs until TASK-039 (ad-hoc) / TASK-040 (project), so a live connection currently returns an `error` frame, not a full run. The protocol/translation/cleanup are unit-tested (`KoboldEndpointProtocolTests`, 90/90 suite green); the live observation needs a real run. Task stays in `review` until TASK-040 lands:**
- [ ] Connect a WS client (e.g. `websocat`) to `/kobold`, send a project-mode payload, observe the ordered `kobold_*` message stream through to `kobold_complete`

## Implementation plan

**Auth (STORY-015 open question — resolved):** mirror `/dragon` + `/wyvern` — the JWT-bearer pipeline (`UseAuthentication` populates `context.User` from the `?token=` query on WS upgrade); reject unauthenticated upgrades with 401 *before* accepting the socket when JWT is enabled, and treat JWT-off local dev as the loopback owner via the existing `ResolveCaller`. No Birko WS middleware.

**Lost-event race (design decision):** the *endpoint* owns the `runId` (allocates a fresh Guid) and **subscribes before** invoking the mode handler — the handler is then told "start a Kobold publishing under this runId." This closes the drop-events-before-subscribe gap in `KoboldRunEventSource` (no subscribers ⇒ events dropped) without persistence/replay.

**Steps:**
1. `KoboldRunRequest` (Server/Models) — initial-payload DTO: `mode` (`adhoc`|`project`) + passthrough fields the handlers will read (kept loose; TASK-039/040 own their shapes).
2. `KoboldWireMessage` (Server/Models) + `From(KoboldRunEvent)` translator → flat camelCase wire frames, nulls omitted. Mapping: endpoint-emitted `kobold_run_started` {runId, worktree, mode}; `ToolCall{Started,Result}` → `kobold_tool_call` (+`phase`); `Reflection` → `kobold_reflect`; `PlanStepUpdated` → `kobold_stream`; `RunCompleted` → `kobold_complete`; `RunError` → `error`.
3. `IKoboldRunModeHandler` seam + `AdHocRunModeHandler` / `ProjectRunModeHandler` **stubs** (throw `NotImplementedException` → relayed as an `error` frame until TASK-039/040). Contract: `StartAsync(KoboldRunRequest, Guid runId, caller, ct) → KoboldRunStartInfo {worktree, mode}`.
4. `KoboldEndpointService.PumpAsync(runId, startInfo, ChannelReader<KoboldRunEvent>, Func<KoboldWireMessage,CancellationToken,Task> send, ct)` — testable core: emit `kobold_run_started`, then translate+send each event; stop on terminal (`RunCompleted`/`RunError`). No WebSocket dependency.
5. `KoboldEndpointService.HandleWebSocketAsync(ws, caller, ct)` — WS glue: receive initial frame → parse → resolve handler by `mode` (unknown → `error`+close) → allocate runId + subscribe → `StartAsync` → run `PumpAsync` against a `WebSocketSender` sink **concurrently** with a receive loop that, on client Close/disconnect, cancels the pump (stops relaying) **without** killing the Kobold (criterion 4). `finally` disposes the subscription.
6. Map `/kobold` in `Program.cs` next to `/dragon` (same 401/400 gates + `ResolveCaller`); add `/kobold` to the `/` health-endpoint list.
7. Register `KoboldEndpointService` + the two handlers in DI.

**Tests** (`DraCode.KoboldLair.Tests`, references Server): `KoboldEndpointProtocolTests` drive `PumpAsync` directly against a real `KoboldRunEventSource` + a list-collecting send — publish ToolCall→Reflection→PlanStep→RunCompleted for the runId and assert the ordered wire stream `kobold_run_started … kobold_complete`; assert `RunError` → `error`; assert disposing the subscription (simulated disconnect) stops relay and does not call `CompleteRun` (Kobold survives).
