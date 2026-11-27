---
id: TASK-208
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

# An answer an agent gives is recorded as a decision the person can see and overturn

## Context

FEATURE-078 D21. Every answer an agent gives instead of the person is recorded as a decision (who answered, and
why). The person sees it for information (Dragon, Needs you) and can overturn it, which reopens the work it affected.

Decisions: FEATURE-078 D21.

## Acceptance criteria

- [x] Every agent answer is stored as a decision naming the agent and its reason
- [x] The person sees agent answers for information in Dragon and the web UI
- [x] Overturning an agent answer records the person's answer and reopens the work it affected

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] Overturn an agent's answer on a throwaway server: the affected task reopens with the person's answer

## Implementation plan

Decided with the owner while planning (FEATURE-078 D21, now `changed` with the detail): the answer is shown as a card in
Dragon and in Needs you until kept or overturned; an overturned task answer revises the task's plan (finished steps
kept); an overturned project answer re-runs the analysis only while no task has started.

1. `PendingDecision` gains `AnsweredBy`, `AnswerReason`, `AgentAnswer`, `ReviewedAt`, `RevisedAt` (columns added at
   startup), with `AwaitsReview` and `IsOverturned`; the store reads unreviewed agent answers, reviews one once, and
   finds overturned answers not yet revised for.
2. Both places an agent answers (a Kobold's `ask_human` in Drake; the analyzer's questions in `DrakeFactory`) record
   who answered and its quote, and raise a `decision_required` card. The card (`AgentAnswerReview`) is the question
   with "Keep the agent's answer" first, a context naming the agent, its answer and reason, and metadata `answeredBy`.
3. `DrakeFactory.ReviewAgentAnswerAsync`: keep, or overturn (the person's answer must be one the question accepts), the
   card retired, the answer sent down the path. A task: its Drake's `ApplyOverturnAsync` reopens it (a running task
   keeps running) and revises the plan through the WrongApproach route; with no Drake, the task reopens in its file and
   the next Drake revises before summoning. The project: `ProjectService.ReanalyzeAfterOverturnAsync`.
4. `DecisionDesk` offers agent answers with the open decisions and routes an answer to one through the review (the
   `/dragon` socket and Warden); Needs you lists them as `agent_answer`; the home view labels them.
5. What the Kobold hears: an overturned answer is "A person overturned the answer <agent> gave ..."; an agent's answer
   is named as the agent's and is not passed to dependent tasks as a person's words.

## Notes

- Red first only as compile failures: the tests use the new API, and I wrote the code before them this time. Two older
  tests that said an agent's answer raises no card now say it raises only the review card.
- The card numbers "Keep" as 1, so a question's own options move down by one on it ("3" is the second original option).
- A task a Kobold is running when its answer is overturned keeps running on the revised plan rather than being
  cancelled (a cancel races the run's own end); it hears the person's answer on its next summon.
- Full .NET suite 715 passed; e2e 188 passed.
- Human test on the throwaway `projects193`: the TASK-205 banner task (Wyvern had answered "What is the banner colour?"
  with "teal" live; its decision marked with who answered, the task set Done). Needs you listed it as `agent_answer`; the
  Dragon socket sent the card with `answeredBy: Overseer`, the reason and "Keep the agent's answer". Answered "crimson":
  "Overturned ... the task reopens", the decision stored crimson with teal kept, the task Unassigned in its file. When
  the project's Drake started it revised the plan first ("Plan revised with 2 new/revised steps", the summary naming
  teal → crimson), the Kobold resumed with "1 new answer(s)", and `banner.py` now prints `KOBOLD [crimson]`, Done again.
  Not seen live: a project-level overturn (covered by tests).
