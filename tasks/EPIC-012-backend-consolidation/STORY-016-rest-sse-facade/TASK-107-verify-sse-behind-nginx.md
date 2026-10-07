---
id: TASK-107
parent: STORY-016
feature: FEATURE-018
status: todo
priority: P3
assignee: human
created: 2026-10-07
depends-on: [TASK-045]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Verify the run events stream survives behind nginx

## Context

Split from TASK-045's human test plan on 2026-10-07: the SSE endpoint works directly (verified live with `curl -N`), and
the proxy settings are documented in `docs/setup-guides/RUN_EVENTS_SSE.md`, but nginx is not installed on the machine
that closed TASK-045, so the stream was never run through a proxy.

## Acceptance criteria

- [ ] With the documented nginx `location` block, a run's stream reaches the client frame by frame (not buffered until the end)
- [ ] A run that is quiet for longer than nginx's default 60 s read timeout keeps its stream open
- [ ] The guide is corrected if anything in it was wrong

## Out of scope

- Other proxies (IIS, Caddy, Traefik)

## Human test plan

- [ ] Run KoboldLair.Server behind nginx with the guide's settings; `curl -N` a run's events through nginx and watch frames arrive as they happen
- [ ] Start a run that pauses over 60 s between events; the stream stays open (heartbeats arrive) until the run ends

## Implementation plan

N/A — a manual check of documented settings.
