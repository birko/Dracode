---
id: TASK-151
parent: STORY-046
feature: FEATURE-026
status: todo
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-150]
blocks: [TASK-149, TASK-152, TASK-160, TASK-164]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Dragon docked in the workspace, following the project

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Dragon is today a full page at `#/dragon` (`src/views/dragon`) with its own message list. Rebuild it as a dock on the
right of the workspace (~360px) built on `b-chat`, talking to `/dragon` through the client host's proxy. It follows the
selected project: opening or switching a project loads that project's conversation (the server already loads the
project's history and resets Dragon's context on a project switch). Streaming (`dragon_stream`), typing and thinking
states show in the chat. The old view and its route go.

Decisions: FEATURE-026 D11, FEATURE-026 D12.

## Acceptance criteria

- [ ] The workspace shows Dragon docked on the right; messages use `b-chat`, with streaming text, typing and thinking states
- [ ] Switching project (URL or header picker) switches Dragon to that project's conversation
- [ ] After a dropped connection, the session resumes and the conversation is replayed (`session_resumed`)
- [ ] The old Dragon view, its route and its manifest are removed
- [ ] An e2e spec with the Dragon socket stubbed covers sending, streaming and the project switch; the suite stays green

## Out of scope

- Resizing with focus (TASK-152)
- Decision cards (TASK-160)
- The home Dragon (TASK-149)

## Human test plan

- [ ] Against a local server with a provider set up: talk to Dragon in project A, switch to B and back — each keeps its own conversation
- [ ] Stop and restart the server mid-conversation: the dock reconnects and shows the history

## Implementation plan

_Populated by `/tasks plan TASK-151` — leave empty until then._
