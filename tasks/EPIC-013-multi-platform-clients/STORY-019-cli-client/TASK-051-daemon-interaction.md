---
id: TASK-051
parent: STORY-019
feature: FEATURE-022
status: blocked
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-050, TASK-049]
blocks: [TASK-052, TASK-053]
pr: null
github-issue: null
jira-key: null
---

# CLI daemon interaction + discovery

## Context

Wire the CLI to the daemon model. Reuses STORY-018's discovery helper (TASK-049: `TryFindLocalDaemon`/`SpawnLocalDaemon`, `--server`). `do`/`run` are ephemeral (spawn server as child, run, exit); `chat`/`analyze` discover or spawn a persistent daemon; `--server <url>` forces a remote daemon (requires OAuth token).

## Acceptance criteria

- [ ] Ephemeral mode: `do`/`run` start `KoboldLair.Server` as a child process, run, then the server exits with the parent
- [ ] Persistent mode: `chat`/`analyze` call `TryFindLocalDaemon`, spawn via `SpawnLocalDaemon` if missing
- [ ] `--server <url>` skips local discovery, talks to the URL, requires a cached OAuth token (TASK-054)
- [ ] Cold-start cost of ephemeral `do` documented; fallback "use daemon if available" noted
- [ ] Tests: discovery hit/miss/spawn paths; `--server` bypasses discovery

## Out of scope

- The verb implementations themselves (TASK-052/053/054)

## Human test plan

- [ ] With no daemon, `koboldlair chat` spawns one; second `chat` reuses it; `koboldlair do` runs ephemerally and leaves no daemon behind

## Implementation plan

_Populated by `/tasks plan TASK-051` — leave empty until then._
