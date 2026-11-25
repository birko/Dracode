---
id: TASK-203
parent: STORY-051
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-201]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# Drake answers a Kobold's question from what its level owns, or passes it up

## Context

FEATURE-078 D18, the supervisor's level. Drake answers only from the task's description, its plan, the project's
constraints and earlier answers; anything else goes up the chain (TASK-201). An answer it gives is recorded (TASK-208).

Decisions: FEATURE-078 D18.

## Acceptance criteria

- [x] Drake answers a question its task, plan, constraints or earlier answers settle, naming which of them settled it
- [x] A question they do not settle goes up the chain unchanged
- [x] Drake never answers from its own judgement beyond those sources

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] A question the plan already answers is answered by Drake without reaching the person; one it does not is passed up

## Implementation plan

Planned at pick (2026-11-25):

1. `Drake.SupervisorSourcesAsync(taskId)`: the task's description, its saved plan, the project's constraints and the
   answers it and the tasks it depends on have (`LoadHumanAnswersAsync`); null for a task the Drake does not hold.
2. `SupervisorLevel` (the chain's Supervisor level): a question already answered for the task (same text, whitespace
   and case aside) is answered from the record without a model; otherwise `SupervisorAnswerAgent` (no tools, one
   turn, answer + word-for-word quote as JSON) answers, kept only when the quote is in the rendered sources and the
   answer is acceptable (`AskHumanQuestion.Accepts`). A project-level question is never the supervisor's.
3. The server registers the level with the Kobold planner's model (`drake-supervisor` in usage records).

## Notes

- **Found in the human test:** the task record holds only the task's title, and `Drake.SetWyvern` has no caller, so a
  Drake in the server never has Wyvern's analysis or Wyrm's recommendation in memory: its sources had neither the task's
  description nor any constraint, and the supervisor declined everything. The sources now read the task's full
  description and the constraints from the project's `analysis.json` and `wyrm-recommendation.json`. The same gap means
  Kobolds never get Drake's "PROJECT CONSTRAINTS" block (`SetProjectConstraints`); not fixed here.
- **Red, after the fact:** with the quote check removed, the not-in-the-sources case failed; with the earlier-answer
  shortcut removed, the repeated-question case failed. 8 `SupervisorLevelTests`; full .NET suite 685 passed.
- **Human test** (throwaway server, vLLM `lan2`), project `greet203b`: the spec states the greeting is Slovak and tells
  the worker to ask the language and whether to end with "!".
  - The language question was answered by Drake ("the task's sources say: "The greeting language is Slovak
    (Ahoj).""), path Supervisor only, no card, the task kept running.
  - The exclamation-mark question, which the sources do not settle, climbed Drake → Wyvern → Dragon → person; answered
    "yes" in Dragon, the task finished Done and `greet.py` prints "Ahoj!".
  - A first run (`greet203`, before the analysis was read) had Drake decline the language question: its sources held
    only the title.
