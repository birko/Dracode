---
id: TASK-170
parent: STORY-025
feature: FEATURE-028
status: todo
priority: P3
assignee: agent
created: 2026-11-16
depends-on: [TASK-021]
blocks: []
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
