---
id: TASK-190
parent: STORY-042
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-037]
pr: 41
github-issue: null
jira-key: null
---

# After Dragon switches project from the chat, the web dock still shows the project that was opened

## Context

Found 2026-11-22 in TASK-180's human test. In waver's workspace the person asked Dragon to have Sage load the waver2
specification. Dragon's spec tool switched the session to waver2 (`DragonService.OnProjectLoadedAsync`; since TASK-180
Dragon's context is waver2's conversation too). The web dock (`DragonConnection`, TASK-151) is never told: it keeps
following waver (`projectId`, the workspace's title, its decision cards), while every message from then on is saved to
waver2's history. Back in waver's workspace later, those messages are not there; they are in waver2's.

The server sends no message for a chat-driven switch; `project_selected` answers only the client's own
`select_project`.

Decided 2026-11-22 (owner): **ask, and stay until the person agrees.** In a workspace (a session whose client
selected a project), a project Dragon loads from the chat does not move the session: the conversation, Dragon's
context, its working folder and where messages are saved stay the opened project's, and Sage can still read or edit
the other project's spec. The dock shows a notice offering to open the other project: accepting opens its workspace
(the client's own `select_project`, so Dragon follows with that project's conversation); declining leaves everything
as it is. This keeps FEATURE-026 D12 (Dragon follows the project the person selects) and needs no ledger change.

## Acceptance criteria

- [x] After a switch made from the chat in a project's workspace, what the dock shows and where the conversation is
      saved agree (per the decision above), proved by a test that fails today
- [x] The decision is recorded in this task (and in FEATURE-026's ledger if it changes D12)
- [x] CLAUDE.md's Dragon session notes match the behaviour

## Out of scope

- The home conversation, which already keeps itself across chat-driven loads (TASK-149)

## Human test plan

- [x] On a throwaway server, in project A's workspace, ask Dragon to have Sage load project B, then send another
      message: the screen and the saved conversation agree on which project it belongs to
  - **Passed 2026-11-22:** see Notes

## Implementation plan

1. Server: `DragonSession.SelectedByClient` (set by `select_project`, cleared by `select_home`). In such a session a
   chat-driven load of another project (`OnProjectLoadedAsync`, and the new-spec path `CheckForNewSpecifications`)
   changes nothing in the session and sends `project_switch_offered { projectId, projectName }` (registered projects
   the caller can see only). A session no client selected for (no web dock) keeps today's switch.
2. Client: `DragonConnection` keeps the offer in its state (`offer`), dropped on any select; the workspace dock shows it
   above the chat with "Open <name>" (navigates to that project's workspace) and "Stay here" (dismisses).
3. Tests: .NET — in a selected session the folder, history and context stay and the offer is sent; e2e — the notice,
   both answers. CLAUDE.md's Dragon session notes.

## Notes

- **Server** (`DragonService`): `DragonSession.SelectedByClient`, set by `select_project` and cleared by
  `select_home`. In such a session `OnProjectLoadedAsync` leaves the session as it is (folder, history, Dragon's
  context and working folder) when Dragon loads another project from the chat, and sends `project_switch_offered
  { projectId, projectName }` (`OfferProjectSwitchAsync`: only a project the caller can see; not saved to the
  conversation). A spec Dragon writes for a new project from a workspace (`CheckForNewSpecifications`) no longer moves
  the session either, and the new project is offered once registered. A session no client selected for keeps the
  chat-driven switch (TASK-180). `GetSession` (internal) lets tests reach a live session.
- **Client:** `DragonConnection` keeps the offer (`state.offer`), drops it on any follow, and `dismissOffer()` declines
  it. The workspace dock shows it above the decision cards: "Dragon loaded <name>. This conversation stays here until
  you open it." with "Open <name>" (to `#/projects/<id>`, where the new dock's `select_project` makes Dragon follow
  with that project's conversation) and "Stay here".
- **Tests:** `DragonProjectSocketTests` +2, both red with the new branch off: in a workspace the session stays (folder,
  history, context) and the offer arrives, then the client's `select_project` switches it; a project the caller cannot
  see is not offered. e2e `dragon-dock.spec.ts` +1 (red before): the notice, "Stay here" keeps the workspace and sends
  nothing, "Open Blog" goes to Blog's workspace and Dragon follows. Suites .NET 564/564, e2e 138/138 (twice).
- **Human test** (throwaway Testing server, scratch copy of `projects160` with its paths moved, LAN model): in waver's
  workspace "have Sage load the waver2 specification" → Dragon answered from waver2's spec and the notice read
  "Dragon loaded waver2…"; "Stay here" hid it and kept the page; "Remember PLUM-7" was saved to waver's conversation
  (waver2 has no saved conversation at all); a second load offered again, "Open waver2" opened waver2's board and the
  server logged "selected project waver2". No console errors. FEATURE-026 D12 (Dragon follows the project the person
  selects) holds as it was, so its ledger is unchanged.
