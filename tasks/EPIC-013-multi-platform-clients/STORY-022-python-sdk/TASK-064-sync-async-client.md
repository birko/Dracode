---
id: TASK-064
parent: STORY-022
feature: FEATURE-025
status: blocked
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-063, TASK-043, TASK-044]
blocks: [TASK-065]
pr: null
github-issue: null
jira-key: null
---

# Python SDK sync + async client

## Context

The `Client` (sync) and `AsyncClient` over the REST facade — projects/tasks/runs CRUD, `wait_for_status`, `from_local_daemon()` (reads `~/.koboldlair/daemon.json` like the CLI). Auth via token or local-daemon bypass.

## Acceptance criteria

- [ ] `Client(server=, token=)` and `AsyncClient` with parity
- [ ] `Client.from_local_daemon()` reads `daemon.json` and connects (loopback bypass)
- [ ] Resource methods: `projects.create/list/get/delete`, `proj.wait_for_status(...)`, `proj.tasks`, `task.run()`
- [ ] Auth header attached; 401 raises a typed error
- [ ] Tests against a mocked `/api/v1` (httpx mock); both sync + async paths

## Out of scope

- SSE streaming (TASK-065); publish (TASK-066)

## Human test plan

- [ ] In a notebook against a local daemon: `Client.from_local_daemon()`, create a project, `wait_for_status("Analyzed")`, list tasks — all return typed models

## Implementation plan

_Populated by `/tasks plan TASK-064` — leave empty until then._
