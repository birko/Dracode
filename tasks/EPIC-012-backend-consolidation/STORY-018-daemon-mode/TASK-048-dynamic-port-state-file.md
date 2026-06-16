---
id: TASK-048
parent: STORY-018
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-047]
blocks: [TASK-049]
pr: null
github-issue: null
jira-key: null
---

# Dynamic port + daemon.json state + absolute ProjectsPath

## Context

The daemon binds an **ephemeral free port** (not the fixed dev port) and records it, plus PID/token/owner/version, in `~/.koboldlair/daemon.json`. Because `KoboldLair.ProjectsPath` defaults to a **relative** `./projects`, the daemon must resolve it to an **absolute** path at startup — otherwise setting the working dir to `~/.koboldlair/` (TASK-047) silently relocates project storage.

## Acceptance criteria

- [ ] Daemon binds an ephemeral free port; writes `{ pid, port, token, startedAt, ownerUser, serverVersion }` to `daemon.json`
- [ ] `ProjectsPath` resolved to an absolute path at startup; verified projects read/write from the original configured location regardless of working dir
- [ ] `daemon.json` removed on graceful stop; survives across a `cd` in the spawning shell
- [ ] `serverVersion` recorded so clients can detect a stale daemon after upgrade
- [ ] Tests: port written matches the listening port; absolute ProjectsPath holds after a working-dir change

## Out of scope

- Reading the file from clients (TASK-049)

## Human test plan

- [ ] Start daemon from a shell `cd`'d into an unrelated folder → confirm projects still land in the configured `ProjectsPath`, and `daemon.json` shows the live ephemeral port

## Implementation plan

_Populated by `/tasks plan TASK-048` — leave empty until then._
