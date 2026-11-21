---
id: TASK-180
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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

- [ ] After Dragon switches project from the chat, its LLM context holds only the new project's conversation (rebuilt
      like `select_project` does, after the reply that announced the switch), proved by a test that fails today
- [ ] `PendingContextReset` is either consumed or removed
- [ ] The chat route and `select_project` key a project's history by the same folder
- [ ] CLAUDE.md's Context Isolation line matches the behaviour

## Out of scope

- The web dock's switch (`select_project`, TASK-151) — already rebuilds the context

## Human test plan

- [ ] On a throwaway server: tell Dragon a codeword in project A, then ask Dragon (in chat) to load project B and ask for
      the codeword — it does not know A's

## Implementation plan

_Populated by `/tasks plan TASK-180` — leave empty until then._
