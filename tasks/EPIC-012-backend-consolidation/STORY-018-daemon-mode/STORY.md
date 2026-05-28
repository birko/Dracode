---
id: STORY-018
parent: EPIC-012
status: planned
created: 2026-05-28
---

# Daemon mode for KoboldLair.Server

## User story

As a CLI user, I want the server to run as a background daemon so that interactive commands (`koboldlair chat`, `koboldlair analyze`) are instant after the first invocation, instead of paying .NET startup latency every time. The daemon should be per-user, discoverable, and cleanly stoppable.

## Behaviour

### New flags on `KoboldLair.Server`

- `--daemon` — detach from terminal, write PID + port + token to `~/.koboldlair/daemon.json`, log to `~/.koboldlair/daemon.log`
- `--stop` — read PID from daemon.json, send SIGTERM (Unix) / Process.Kill (Windows), remove daemon.json
- `--status` — read daemon.json, check process alive, print state

### Daemon state file

`~/.koboldlair/daemon.json`:

```json
{
  "pid": 4711,
  "port": 17091,
  "token": "local-trust-bypass",
  "startedAt": "2026-05-28T10:14:22Z",
  "ownerUser": "<os-user>",
  "serverVersion": "1.0.0"
}
```

- Lock file `~/.koboldlair/daemon.lock` to prevent multiple daemons per user (open with `FileShare.None`)
- Health endpoint `GET /` already exists; daemon health-check uses it during startup wait
- Graceful shutdown: existing `GracefulShutdownCoordinator` drains in-flight Kobolds, then removes daemon.json

### Client-side discovery

Library helper in `DraCode.KoboldLair` (or new `DraCode.KoboldLair.Discovery`) used by all clients:

```csharp
DaemonInfo? TryFindLocalDaemon();  // reads ~/.koboldlair/daemon.json, checks alive
DaemonInfo SpawnLocalDaemon();     // forks KoboldLair.Server --daemon, waits for ready
```

`--server` flag on clients bypasses local-daemon lookup, talks to the URL directly (must auth via STORY-017).

## Cross-platform notes

- Detach mechanics differ per OS: on Windows, spawn with `CREATE_NO_WINDOW` + `DETACHED_PROCESS`; on Unix, double-fork or `start-stop-daemon`-style
- Log file rotation: simple roll at 10 MB to `daemon.log.1`, keep 3 generations
- Daemon's working directory should be `~/.koboldlair/` (not the cwd of the spawner) so a `cd` in the CLI doesn't strand the daemon

## Risks

- Stale `daemon.json` after a `kill -9`: PID alive check in `TryFindLocalDaemon` handles it, but document recovery (`koboldlair stop --force` to clean up)
- User has two terminal windows, both run `koboldlair chat` at once → race for daemon spawn. Lock file resolves; the loser just connects to the winner's daemon.
