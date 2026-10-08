---
id: TASK-140
parent: STORY-040
feature: null
status: todo
priority: P3
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-542 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Proxy /dragon and /wyvern in the client host with the Birko WebSocket proxy

## Context

`DraCode.KoboldLair.Client/Program.cs` (190): two duplicated proxy handlers + `RelayWebSocketAsync`. The security defects are fixed first in TASK-119.

Framework side: Birko TASK-542 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Both routes mapped with the framework proxy
- [ ] Local relay code deleted
- [ ] Certificate validation stays on

## Out of scope

- The framework change itself (Birko TASK-542)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
