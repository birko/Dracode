---
id: TASK-044
parent: STORY-016
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-042, TASK-037]
blocks: [TASK-045]
pr: null
github-issue: null
jira-key: null
---

# Runs endpoints (start + status)

## Context

`POST /api/v1/runs` starts an ad-hoc Kobold run and returns a `runId`; `GET /api/v1/runs/{id}` returns current status. The actual run uses the same Kobold machinery as STORY-015 and publishes to TASK-037's run-event source (which TASK-045's SSE then streams). One run engine, reachable from WS (`/kobold`) and REST.

## Acceptance criteria

- [ ] `POST /api/v1/runs` accepts a run spec, starts the run, returns `{ runId }` immediately
- [ ] `GET /api/v1/runs/{id}` returns status (pending/running/completed/failed) + summary
- [ ] Runs publish to the TASK-037 event source (consumed by TASK-045)
- [ ] Ownership: a run is owned by its caller; scoping applies
- [ ] Tests: start → status transitions; unauthorized caller can't read another's run

## Out of scope

- The SSE event stream (TASK-045)
- Ad-hoc git/worktree specifics already covered by STORY-015 machinery

## Human test plan

- [ ] `curl POST /api/v1/runs` → get a `runId`; poll `GET /api/v1/runs/{id}` until completed

## Implementation plan

_Populated by `/tasks plan TASK-044` — leave empty until then._
