---
id: TASK-151
parent: STORY-046
feature: FEATURE-026
status: done
priority: P1
assignee: agent
created: 2026-11-16
depends-on: [TASK-150]
blocks: [TASK-149, TASK-152, TASK-160, TASK-164]
findings: []
pr: 15
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

- [x] The workspace shows Dragon docked on the right; messages use `b-chat`, with streaming text, typing and thinking states
- [x] Switching project (URL or header picker) switches Dragon to that project's conversation
- [x] After a dropped connection, the session resumes and the conversation is replayed (`session_resumed`)
- [x] The old Dragon view, its route and its manifest are removed
- [x] An e2e spec with the Dragon socket stubbed covers sending, streaming and the project switch; the suite stays green

## Out of scope

- Resizing with focus (TASK-152)
- Decision cards (TASK-160)
- The home Dragon (TASK-149)

## Human test plan

- [x] Against a local server with a provider set up: talk to Dragon in project A, switch to B and back — each keeps its own conversation
- [x] Stop and restart the server mid-conversation: the dock reconnects and shows the history

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **The old view was a mock.** `dragon-view.ts` never connected to `/dragon` (canned replies), so the dock is the first
  real web client of the socket. With it gone, a project cannot be started from the web UI until the home Dragon
  (TASK-149); the homepage's empty state no longer links to `#/dragon`.
- **Server: the project switch did not exist.** A session changed project only when Dragon's spec tool loaded one from
  something said in chat. New `select_project { projectId }` (`DragonService.SelectProjectAsync`): only a project the
  caller can see (else an `error` with `errorType: project_not_found`); waits for the session's running Dragon request
  (`DragonRequestQueue.WaitForSessionIdleAsync`: Dragon's call takes no cancellation and rewrites its conversation
  when it ends); loads the project's saved conversation; makes it the session history and Dragon's LLM context
  (`ConversationFromHistory`: starts with the user, alternates, ends with Dragon); points Dragon at the project folder
  (`Paths.Output`); answers `project_selected` and replays the conversation (`isReplay`, the same replay as a resume).
  A selected project's conversation replaces the welcome (`WelcomeSent`).
- **Server: Dragon's replies were never saved.** Only the user's messages and the welcome reached the history; every
  saved conversation in the scratch databases had exactly one `dragon_message`. `DragonRequest.OnResponse` now records
  each reply before it is sent, so it is kept even when the client is gone.
- **Server: history was saved to SQL but read from the file.** `TrackMessage` writes to the `dragon_history` table;
  the project-load paths read only `dragon-history.json`. `LoadProjectHistoryAsync` reads the table when there is one
  (both existing load sites use it too).
- **Client host: the proxy dropped `sessionId`.** `ServerWebSocketProxy` forwarded only `token`, so a reconnect through
  the host always started a new session and `session_resumed` could never happen. Found by the human test; it now
  forwards `sessionId` too (`BuildUpstreamUri`).
- **Client:** `services/dragon-connection.ts` is the page's one `/dragon` connection (same origin, through the proxy; on
  Birko's `WsClient`, whose options object it updates with the `sessionId`), so opening another project keeps the
  session and only sends `select_project`. Until `project_selected` arrives, live traffic is the project being left's
  and is dropped. On reconnect it waits for `session_resumed` (replay) or `session_not_found` (selects again), falling
  back to selecting again after 1.5 s; meanwhile the old chat stays visible but closed. `views/dragon-dock.ts` renders
  it on `b-chat` (assistant Markdown through the sanitiser, streamed text growing in place, typing, a status line with
  what Dragon or a council member is doing). The workspace lays it out on the right at 360px (sticky), under the tabs
  below 56rem; the Spec tab's columns now follow its own width (container query).
- **Tests:** .NET `DragonProjectSocketTests` (select + replay + switch, another user's project, reconnect resumes; all 3
  red on the old server) and `DragonConversationTests` (reply recorded with the client gone, session idle only after
  its request, conversation rebuilt from history), plus a proxy test for `sessionId` (red first). Full suite 495/495
  after the proxy test. e2e `dragon-dock.spec.ts`, 10 tests against a fake `/dragon` (`page.routeWebSocket`), all red
  on the old client; `workspace.spec.ts` holds the socket open silently; suite 53/53.
- **Human test** (throwaway Testing server on a scratch copy, JWT on, LAN `finstat` model): in `waver` Dragon took a
  codeword, in `waver2` another; back in `waver` its whole conversation was there, Dragon's replies included, and Dragon
  answered the codeword `ALPHA-7`. Server restarted mid-conversation: the dock showed "reconnecting…", got
  `session_not_found`, opened the project again, showed the history and still knew `BRAVO-3`. Client host restarted
  with the server up: `session_resumed` and the conversation replayed. Light and dark at 1400px and 375px: no
  horizontal scroll, no console errors.
- **Left for later:** the chat-driven switch (`OnProjectLoadedAsync`) sets a `PendingContextReset` nothing reads, so
  its LLM context carries over; `DragonRequestQueue.Dispose` throws `TaskCanceledException` when it stops its workers
  (the new tests swallow it); Birko's `b-chat` puts its `placeholder` into markup unescaped.
