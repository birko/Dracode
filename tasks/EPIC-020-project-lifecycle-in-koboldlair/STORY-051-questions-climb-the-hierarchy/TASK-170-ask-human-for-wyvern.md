---
id: TASK-170
parent: STORY-051
feature: FEATURE-078
status: todo
priority: P3
assignee: agent
created: 2026-11-16
depends-on: [TASK-021]
blocks: [TASK-204]
findings: []
pr: null
github-issue: null
jira-key: null
---

# ask_human for the analyzer agent (Wyvern) — needs a project-level wait

## Context

Deferred part of FEATURE-028 D4 (changed by the owner 2026-11-16 to "Kobold only"). TASK-021 gives Kobolds an
`ask_human` tool that parks the calling task (TASK-019's `ParkTaskAsync`). Wyvern, the analyzer, has no task to park:
it analyzes a whole project in `WyvernProcessingService`, between the `WyrmAssigned` and `Analyzed` project states.
A Wyvern question needs a way for that analysis to wait for a person and resume, which nothing provides yet.

Unblock condition (D7): a project-level wait exists — the analysis can be suspended with its state saved, the project
held out of `WyvernProcessingService`'s pick-up, and resumed with the answer.

## Decisions (owner, 2026-11-25 — FEATURE-078 D26)

Re-homed from FEATURE-028 D7 to FEATURE-078 (questions climb the agent hierarchy), as its first slice.

- **How Wyvern asks:** its analysis may carry a `questions` list (the `ask_human` shape: question, options, free
  text). If it is not empty, the decisions are stored and the project waits; once they are answered, the analysis runs
  again with the answers in its prompt. No saved conversation is resumed: the analysis is one call.
- **How the project waits:** `WyvernProcessingService` skips a project with an open project-level decision, read from
  the decision store. No new project status.
- **How many:** at most three questions per analysis, one round; the re-run must produce tasks and may not ask again
  (a changed specification starts a new analysis).
- **Timeout:** none; the project waits until answered.
- **Who answers:** the person, until the question chain (FEATURE-078 D18, D24) exists; then the question goes through
  Dragon's level first.
- A project-level decision is stored with an empty task id and `Source = AskHuman`; its card says the analysis will
  resume.

## Acceptance criteria

- [ ] Wyvern can call `ask_human`; the project's analysis waits without blocking other projects, and resumes with the answer in its context
- [ ] The wait survives a server restart, as a parked task's does
- [ ] Wyrm still has no `ask_human`
- [ ] Tests cover the wait, the restart and the resume

## Out of scope

- Kobold's `ask_human` (TASK-021)

## Human test plan

- [ ] Give Wyvern a specification with a real ambiguity, answer its question in Dragon, and confirm the analysis resumes and uses the answer

## Implementation plan

_Populated by `/tasks plan TASK-170` — leave empty until then._
