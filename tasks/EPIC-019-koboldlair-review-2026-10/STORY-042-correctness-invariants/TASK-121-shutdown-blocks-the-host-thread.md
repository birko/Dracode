---
id: TASK-121
parent: STORY-042
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: 42
github-issue: null
jira-key: null
findings: [CR-2]
---

# Graceful shutdown blocks the host thread with Thread.Sleep inside ApplicationStopping

## Context

`DraCode.KoboldLair.Server/Program.cs` L917 calls `Thread.Sleep(shutdownCoordinator.GracePeriod)` (10 s) inside the
`ApplicationStopping` callback. That blocks the host's stopping thread and counts against `ShutdownTimeout`, so hosted
services get less time, not more. `Services/GracefulShutdownCoordinator.cs` (54) duplicates what
`IHostApplicationLifetime` / `IHostedLifecycleService` already provide.

Found in the 2026-10-08 KoboldLair review.

## Acceptance criteria

- [x] No blocking wait in a lifetime callback; drain happens in a hosted service's `StopAsync` (or `StoppingAsync`)
- [x] `HostOptions.ShutdownTimeout` covers the grace period
- [x] `GracefulShutdownCoordinator` removed or reduced to what the host does not provide

## Human test plan

- [x] Ctrl+C with a Kobold mid-run: in-flight work drains, the process exits within the configured timeout.
  - **Passed 2026-11-22:** run by the owner; see Notes

## Implementation plan

1. `DrakeExecutionService` passes its stopping token to `Drake.ExecuteTaskAsync`, so the host's stop cancels the runs
   and waits for the cycle (`Task.WhenAll` over the runs) to end.
2. `ShutdownFlushService` (an `IHostedService`, registered before the background services so it stops after them)
   saves Drakes' channels, the planning contexts and the notifications in `StopAsync`.
3. `HostOptions.ShutdownTimeout` from `KoboldLair:Shutdown:TimeoutSeconds` (default 30).
4. Remove the `ApplicationStopping` callback and `GracefulShutdownCoordinator`.

## Notes

- **Fix:** the `ApplicationStopping` callback (with its `Thread.Sleep(10 s)` and sync-over-async flushes) and
  `GracefulShutdownCoordinator` are gone; the host provides both what the coordinator did (a stopping token) and the
  wait. Runs take `DrakeExecutionService`'s stopping token; `ShutdownFlushService.StopAsync` does the saving, after the
  background services have stopped; `HostOptions.ShutdownTimeout` (`KoboldLair:Shutdown:TimeoutSeconds`, default 30)
  bounds all of it. The flush service takes its dependencies in the constructor: resolving them in `StopAsync` failed
  under the test host, which had disposed its provider by then (caught by `HostStartupTests`).
- **Tests:** `GracefulShutdownTests` (3): the flush service is registered before `DrakeExecutionService`,
  `DrakeMonitoringService` and `ReasoningMonitorService` (so it stops after them); `ShutdownTimeout` comes from
  configuration and defaults to 30 s; the flush finishes promptly and a spent timeout is logged, not thrown. Suites
  .NET 567/567 — and about half as long (2.5 min, was 5), since every test host that stopped used to sleep 10 s in the
  old callback — e2e 138/138 (one run hit the TASK-189 flake).
- **Human test** (owner's Ctrl+C; throwaway Testing server on a scratch copy of `projects160` with its paths moved;
  the LAN model behind a local proxy delaying each call 15 s, `slowproxy.py`, so the run was mid-call): the log reads
  "Application is shutting down" → "Graceful shutdown requested for Kobold …" → the Kobold saved its plan (1/2 steps,
  kept in progress; the task file reads Working) → "DrakeExecutionService stopped" → the flush (Drake channel,
  planning contexts, notifications) → "Graceful shutdown complete". The process exited 2.0 s after Ctrl+C (it used to
  sleep 10 s). The proxy logged the cancelled call as an aborted connection, as expected.
- **Seen while testing:** a run stopped by the shutdown is logged as "reached iteration limit - work incomplete", the
  wrong reason → TASK-191.
