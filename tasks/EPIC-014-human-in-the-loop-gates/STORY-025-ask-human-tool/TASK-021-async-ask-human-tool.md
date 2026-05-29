---
id: TASK-021
parent: STORY-025
feature: null
status: todo
priority: P1
assignee: ai
created: 2026-05-29
depends-on: [TASK-019]
blocks: [TASK-022]
pr: null
github-issue: null
jira-key: null
---

# Implement async `ask_human` tool

## Context

The interactive `AskUserTool` is wired only for the Dragon CLI (`DraCode/Program.cs:508`) and blocks on a console prompt — unusable in background agents. Build a non-blocking `ask_human` tool for automatic agents (Kobold, Wyvern) that posts a structured question and parks the task using the TASK-019 plumbing. The tool follows the project's tool conventions (async `ExecuteAsync` only — `Tool` base is async-only as of TASK-018; docs live on the `Tool` class per the ACI principle).

## Acceptance criteria

- [ ] New `AskHumanTool` in `DraCode.KoboldLair/Agents/Tools/`, `ExecuteAsync`-only, with self-describing tool docs.
- [ ] Parameters: `question` (string), `options` (string[]?, optional choices), `allow_free_text` (bool), `context` (string?, why it's asking).
- [ ] Execution writes a `PendingDecision` (intent = question, not escalation) and parks the calling task (reuses TASK-019 mechanism).
- [ ] Registered in the Kobold and Wyvern tool catalogues; **not** registered for Wyrm.
- [ ] Gated by policy hooks (`allowAskHuman`, `maxQuestionsPerTask`) — read defensively so it still works before TASK-023 lands (default allow, no cap).
- [ ] Answer with `options` set is validated against the options; free-text passes through verbatim.
- [ ] Tool call + pending decision captured in the plan execution log.

## Out of scope

- Delivering the answer back into agent context on resume — TASK-022.
- Dragon-side rendering/answering — TASK-024.
- Policy enforcement implementation — TASK-023 (only the hooks are read here).

## Human test plan

- [ ] Have a Kobold call `ask_human` with two options; confirm the task parks and the question is persisted with its options.
- [ ] Confirm Wyrm has no access to the tool.

## Implementation plan

_Populated by `/tasks plan TASK-021` — leave empty until then._
