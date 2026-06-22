---
id: TASK-049
parent: STORY-018
feature: FEATURE-020
status: blocked
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-048, TASK-032]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Client discovery helper + loopback auth-bypass

## Context

Shared library helper (in `DraCode.KoboldLair` or a new `DraCode.KoboldLair.Discovery`) used by all clients to find/spawn a local daemon, plus wiring the loopback auth-bypass token. **Consumed by STORY-019 (CLI)** and other EPIC-013 clients — design for reuse. The `"local-trust-bypass"` token in `daemon.json` is the concrete handle for TASK-032's loopback bypass.

## Acceptance criteria

- [ ] `DaemonInfo? TryFindLocalDaemon()` — reads `daemon.json`, checks PID alive + `GET /`
- [ ] `DaemonInfo SpawnLocalDaemon()` — forks `server --daemon`, waits for ready
- [ ] `--server <url>` client flag bypasses local lookup, talks to URL directly (auths via STORY-017)
- [ ] Loopback bind → bypass token honoured by TASK-032; **non-loopback bind never gets the bypass**
- [ ] Stale `daemon.json` (post `kill -9`) handled; `stop --force` recovery path documented
- [ ] Two concurrent spawns: lock-file loser connects to the winner's daemon
- [ ] Tests: discovery against a live + a stale state file; bypass only under loopback

## Out of scope

- The CLI verbs that call this (STORY-019)

## Human test plan

- [ ] With a daemon running, a fresh client call auto-discovers it (no spawn); kill -9 the daemon, call again → stale file detected and a new daemon spawned; confirm a non-loopback bind still requires a token

## Implementation plan

_Populated by `/tasks plan TASK-049` — leave empty until then._
