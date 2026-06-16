---
id: FEATURE-017
created: 2026-05-31
owner: human
status: idea
---

# /kobold WebSocket endpoint — ad-hoc + project-scoped Kobold execution

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

The current backend only talks to browsers over a streaming connection tied to the full project pipeline. Command-line users and scripts that just want to run a single worker against a folder, or against one already-planned task, have no clean entry point. The old single-agent server did this, but it is being retired (see FEATURE-016), so the capability needs a proper home on the current backend.

## Proposed shape

Add a single endpoint on the current backend that runs one worker for the caller in two modes:

- **Ad-hoc:** point it at a folder and a request. It makes sure the folder is version-controlled, works in an isolated copy, streams its progress back live, and hands back a result the user can review and merge themselves.
- **Project:** point it at an existing project task. It reuses the project's existing plan and isolated workspace, streams progress the same way, and commits to the project's feature branch when done.

Either way the caller gets live updates (tool calls, self-checks, completion) so non-browser clients can show progress immediately.

## Out of scope (initial)

- The REST/non-streaming surface (that is FEATURE-018)
- Authentication and per-caller identity (that is FEATURE-019)
- Auto-applying ad-hoc results back into the user's folder — the user runs the merge themselves

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
