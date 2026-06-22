---
id: TASK-022
parent: STORY-025
feature: FEATURE-028
status: blocked
priority: P2
assignee: ai
created: 2026-05-29
depends-on: [TASK-021]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Deliver human answer back into agent context on resume

## Context

When a parked `ask_human` (TASK-021) or human-decision (TASK-020) is answered, the agent must resume with the answer in-band so its next reasoning step uses it. The Kobold already supports conversation checkpoint save/resume (`Kobold.cs:1200-1238`); this task injects the answer as a synthetic tool-result / user message at resume time. This is the round-trip that makes `ask_human` actually useful and is the same channel STORY-029 reuses for escalation-resolution payloads.

## Acceptance criteria

- [ ] On decision resolution, the answer is injected into the resumed agent's conversation as a synthetic message (tool-result for `ask_human`, user message for a decision) clearly attributed to the human.
- [ ] Resume picks up from the saved plan/conversation checkpoint with the answer present before the next LLM call.
- [ ] Works for both Kobold and Wyvern parked tasks.
- [ ] Choice answers are delivered as the chosen option value; free-text delivered verbatim.
- [ ] Test: a Kobold that asked a 2-option question resumes and its next action reflects the chosen option.

## Out of scope

- The Dragon UI that collects the answer — TASK-024.
- Escalation-resolution feedback content — STORY-029/TASK-026 (reuses this channel).

## Human test plan

- [ ] End-to-end: Kobold asks → answer submitted via repository/test hook → Kobold resumes and visibly acts on the answer in its next reflection.

## Implementation plan

_Populated by `/tasks plan TASK-022` — leave empty until then._
