---
id: TASK-190
parent: STORY-042
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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
pr: null
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

Open decision: which way round to fix it — (a) the server tells the client (`project_selected` for the new project,
or a new message) and the dock follows, opening that project's workspace or offering to; or (b) a session whose
client selected a project keeps that project, and the chat-driven switch only reads the other project (no folder,
history or context change), as the home conversation already does since TASK-149.

## Acceptance criteria

- [ ] After a switch made from the chat in a project's workspace, what the dock shows and where the conversation is
      saved agree (per the decision above), proved by a test that fails today
- [ ] The decision is recorded in this task (and in FEATURE-026's ledger if it changes D12)
- [ ] CLAUDE.md's Dragon session notes match the behaviour

## Out of scope

- The home conversation, which already keeps itself across chat-driven loads (TASK-149)

## Human test plan

- [ ] On a throwaway server, in project A's workspace, ask Dragon to have Sage load project B, then send another
      message: the screen and the saved conversation agree on which project it belongs to

## Implementation plan

_Populated by `/tasks plan TASK-190` — leave empty until then._
