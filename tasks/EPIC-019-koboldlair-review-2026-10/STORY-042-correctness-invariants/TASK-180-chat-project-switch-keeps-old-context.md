---
id: TASK-180
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-21
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-030]
pr: null
github-issue: null
jira-key: null
---

# A project switch made from the chat keeps the old project's conversation in Dragon's context

## Context

Found 2026-11-21 while building TASK-151. When Dragon's spec tool loads another project from something said in chat
(`SpecificationManagementTool` → `DragonService.OnProjectLoadedAsync`), the session's history is cleared and the new
project's saved conversation loaded, and `session.PendingContextReset` is set — but nothing reads that flag. Dragon's
LLM conversation (`DragonAgent._conversationHistory`) therefore still holds the old project's exchange, and its next
answers can mix the two projects. CLAUDE.md's "switching projects clears LLM conversation history" held for neither
route until TASK-151's `select_project`, which rebuilds the context itself (`ConversationFromHistory`).

The chat route also keys history by whatever folder the tool resolved (the spec's directory or
`ProjectsPath/<sanitized name>`), where `select_project` uses `project.Paths.Output`; `SqlHistoryRepository` normalises
the path, so this only matters if the two ever name different folders for one project.

## Acceptance criteria

- [x] After Dragon switches project from the chat, its LLM context holds only the new project's conversation (rebuilt
      like `select_project` does, after the reply that announced the switch), proved by a test that fails today
- [x] `PendingContextReset` is either consumed or removed
- [x] The chat route and `select_project` key a project's history by the same folder
- [x] CLAUDE.md's Context Isolation line matches the behaviour

## Out of scope

- The web dock's switch (`select_project`, TASK-151) — already rebuilds the context

## Human test plan

- [x] On a throwaway server: tell Dragon a codeword in project A, then ask Dragon (in chat) to load project B and ask for
      the codeword — it does not know A's
  - **Passed 2026-11-22:** see Notes

## Implementation plan

1. `OnProjectLoadedAsync` finds the registered project first and keys the session, Dragon's working folder and the
   history by its `Paths.Output` (paths compared full, without a trailing separator, any case).
2. `ApplyPendingContextReset(session)`: called from the request's `OnResponse`, after the reply is recorded and after
   Dragon's own write-back of the conversation; rebuilds the context from the session history (the new project's
   conversation and the reply that announced the switch) with `ConversationFromHistory`, and clears the flag.
   `select_project` and `select_home` clear it, as they rebuild the context themselves.
3. Tests in `DragonProjectSocketTests`; CLAUDE.md's Context Isolation line.

## Notes

- **Fix** (`DragonService`): the chat-driven switch's flag is now consumed. `ApplyPendingContextReset` runs in
  `OnResponse`, after Dragon's `ContinueSessionAsync` has written its conversation back (the only point where a reset
  sticks), and rebuilds Dragon's LLM context from the session history: the new project's saved conversation and the
  reply that announced the switch (joined with its last Dragon turn, as `ConversationFromHistory` joins consecutive
  turns). The request's own user message stays in the old project's history, as before. A request that fails gets no
  `OnResponse`; the flag then applies after the next reply. `select_project` / `select_home` clear the flag.
- **Folder key:** `OnProjectLoadedAsync` now looks the project up first and uses its `Paths.Output`, the key
  `select_project` uses; it compares paths without a trailing separator and in any case (`SameFolder`). Before, the
  folder came from the spec tool (`ProjectsPath/<sanitized name>` or the spec's folder), which could name the same
  folder differently. `DragonAgent.Conversation` (internal) exposes the context to the tests.
- **Tests:** `DragonProjectSocketTests` +2, both red with the fix turned off: after a chat switch Dragon's context
  holds only the new project's conversation and the flag is consumed; the chat route keys the project by its
  `Paths.Output` when the tool names it with another case and a trailing separator. Suites .NET 562/562, e2e 137/137
  (twice).
- **Human test** (throwaway Testing server on a scratch copy of `projects160` with its stored paths moved to the copy,
  LAN model): in waver's workspace "Remember this codeword: TANGERINE-41" (confirmed), then "have Sage load the waver2
  specification" (the server logged the switch and "Dragon's context rebuilt"), then "What codeword did I give you
  earlier?" → "NO CODEWORD". No console errors.
- **Seen while testing:** the web dock keeps following the project that was opened after a chat switch, while the
  conversation is saved to the new project → TASK-190.
