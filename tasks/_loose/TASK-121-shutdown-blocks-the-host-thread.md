---
id: TASK-121
parent: null
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Graceful shutdown blocks the host thread with Thread.Sleep inside ApplicationStopping

## Context

`DraCode.KoboldLair.Server/Program.cs` L917 calls `Thread.Sleep(shutdownCoordinator.GracePeriod)` (10 s) inside the
`ApplicationStopping` callback. That blocks the host's stopping thread and counts against `ShutdownTimeout`, so hosted
services get less time, not more. `Services/GracefulShutdownCoordinator.cs` (54) duplicates what
`IHostApplicationLifetime` / `IHostedLifecycleService` already provide.

Found in the 2026-10-08 KoboldLair review.

## Acceptance criteria

- [ ] No blocking wait in a lifetime callback; drain happens in a hosted service's `StopAsync` (or `StoppingAsync`)
- [ ] `HostOptions.ShutdownTimeout` covers the grace period
- [ ] `GracefulShutdownCoordinator` removed or reduced to what the host does not provide

## Human test plan

Ctrl+C with a Kobold mid-run: in-flight work drains, the process exits within the configured timeout.

## Implementation plan
