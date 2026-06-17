---
id: TASK-047
parent: STORY-018
feature: FEATURE-020
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: []
blocks: [TASK-048]
pr: null
github-issue: null
jira-key: null
---

# Daemon lifecycle flags

## Context

`--daemon` / `--stop` / `--status` flags on `KoboldLair.Server`. Detach from terminal, PID + lock file, log rotation. `--stop` hooks into the **existing `GracefulShutdownCoordinator`** (confirmed present) to drain in-flight Kobolds before exit. `--status` reuses the existing `GET /` health endpoint.

## Acceptance criteria

- [ ] `--daemon` detaches (Windows: `CREATE_NO_WINDOW`+`DETACHED_PROCESS`; Unix: double-fork), writes PID, logs to `~/.koboldlair/daemon.log`
- [ ] `--stop` reads PID, drains via `GracefulShutdownCoordinator`, terminates, removes state
- [ ] `--status` checks PID liveness + `GET /`, prints state
- [ ] `~/.koboldlair/daemon.lock` (`FileShare.None`) prevents a second daemon per user
- [ ] Log rotation: roll at 10 MB to `daemon.log.1`, keep 3 generations
- [ ] Working directory set to `~/.koboldlair/` (not spawner cwd)
- [ ] Tests: lifecycle transitions; lock blocks a second daemon

## Out of scope

- Dynamic port + state-file contents (TASK-048)
- Discovery helper + auth bypass (TASK-049)

## Human test plan

- [ ] `server --daemon` → detaches, returns terminal; `--status` shows running; `--stop` drains and removes the lock; second `--daemon` while one runs is refused by the lock

## Implementation plan

_Populated by `/tasks plan TASK-047` — leave empty until then._
