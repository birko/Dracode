---
id: STORY-018
parent: EPIC-012
status: planned
created: 2026-05-28
---

# Daemon mode for KoboldLair.Server

## User story

As a CLI user, I want the server to run as a background daemon so that interactive commands (`koboldlair chat`, `koboldlair analyze`) are instant after the first invocation, instead of paying .NET startup latency every time. The daemon should be per-user, discoverable, and cleanly stoppable.

## Existing building blocks (verified)

- ✅ **`GracefulShutdownCoordinator`** exists (`DraCode.KoboldLair.Server/Services/`) and already drains in-flight Kobolds — daemon `--stop` hooks into it before removing the state file.
- ✅ **`GET /` health endpoint** exists (`Program.cs`) — the daemon startup-wait and `--status` reuse it.

The rest (state file, lock file, detach mechanics, discovery helper) is net-new.

## Behaviour

### New flags on `KoboldLair.Server`

- `--daemon` — detach from terminal, write PID + port + token to `~/.koboldlair/daemon.json`, log to `~/.koboldlair/daemon.log`
- `--stop` — read PID from daemon.json, drain via `GracefulShutdownCoordinator`, then SIGTERM (Unix) / `Process.Kill` (Windows), remove daemon.json
- `--status` — read daemon.json, check process alive (PID liveness), hit `GET /`, print state

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
- **Dynamic port selection**: the daemon binds an ephemeral free port (not the fixed dev port) and writes it to `daemon.json`; clients read it. ⚠️ Net-new — the server currently binds a fixed/Aspire-assigned port.
- Graceful shutdown: `GracefulShutdownCoordinator` drains in-flight Kobolds, then removes daemon.json

### Auth interplay with STORY-017

The `"token": "local-trust-bypass"` is the concrete handle for **STORY-017's local-daemon bypass**: when the daemon binds `127.0.0.1`, JWT validation is skipped and the OS user is trusted. Keep this story's daemon.json and STORY-017's loopback-bypass rule in lockstep — a non-loopback bind must *not* get the bypass token.

### Client-side discovery

Library helper in `DraCode.KoboldLair` (or new `DraCode.KoboldLair.Discovery`) used by all clients:

```csharp
DaemonInfo? TryFindLocalDaemon();  // reads ~/.koboldlair/daemon.json, checks PID alive + GET /
DaemonInfo SpawnLocalDaemon();     // forks KoboldLair.Server --daemon, waits for ready
```

`--server` flag on clients bypasses local-daemon lookup, talks to the URL directly (must auth via STORY-017). This helper is **consumed by STORY-019 (CLI)** and the other EPIC-013 clients — design its surface for cross-client reuse.

## Cross-platform notes

- Detach mechanics differ per OS: on Windows (this dev environment), spawn with `CREATE_NO_WINDOW` + `DETACHED_PROCESS`; on Unix, double-fork or `start-stop-daemon`-style.
- Log file rotation: simple roll at 10 MB to `daemon.log.1`, keep 3 generations.
- Daemon's working directory should be `~/.koboldlair/` (not the spawner's cwd) so a `cd` in the CLI doesn't strand the daemon. ⚠️ Because `KoboldLair.ProjectsPath` defaults to a **relative** `./projects`, the daemon must resolve `ProjectsPath` to an **absolute** path at startup — otherwise switching the working dir to `~/.koboldlair/` silently relocates where projects are read/written.

## Suggested task breakdown

1. **Daemon lifecycle** — `--daemon`/`--stop`/`--status` flags, detach, PID/lock file, log rotation; hook `--stop` into `GracefulShutdownCoordinator`.
2. **Dynamic port + state file** — ephemeral bind, write/read `daemon.json`, absolute `ProjectsPath` resolution.
3. **Discovery helper** — `TryFindLocalDaemon` / `SpawnLocalDaemon` (placement decision: `DraCode.KoboldLair` vs new `.Discovery`), `--server` override.
4. **Auth bypass wiring** — loopback-only trust token, in lockstep with STORY-017.

## Dependencies

- Couples with **STORY-017** for the loopback auth-bypass semantics (the daemon.json token is the bypass handle).
- The discovery helper is **consumed by STORY-019 (CLI)** and other EPIC-013 clients.

## Risks

- Stale `daemon.json` after a `kill -9`: PID liveness check in `TryFindLocalDaemon` handles it, but document recovery (`koboldlair stop --force` to clean up).
- User has two terminals both running `koboldlair chat` at once → race for daemon spawn. Lock file resolves it; the loser connects to the winner's daemon.
- Daemon outlives a server upgrade: `serverVersion` in daemon.json lets a newer client detect a stale daemon and offer to restart it.
