---
id: TASK-170
parent: STORY-051
feature: FEATURE-078
status: done
priority: P3
assignee: agent
created: 2026-11-16
depends-on: [TASK-021]
blocks: [TASK-204]
findings: []
pr: 72
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

- [x] Wyvern can call `ask_human`; the project's analysis waits without blocking other projects, and resumes with the answer in its context
- [x] The wait survives a server restart, as a parked task's does
- [x] Wyrm still has no `ask_human`
- [x] Tests cover the wait, the restart and the resume

## Out of scope

- Kobold's `ask_human` (TASK-021)

## Human test plan

- [x] Give Wyvern a specification with a real ambiguity, answer its question in Dragon, and confirm the analysis resumes and uses the answer

## Implementation plan

Planned at pick (2026-11-25), from the decisions above. Wyvern's analysis is one model call that returns JSON
(`WyvernAgent.AnalyzeSpecificationAsync`, `maxIterations: 1`); nothing in it can wait, so the wait lives around it.

1. **Red first.**
   - `Wyvern` (canned-reply harness): a reply that carries `questions` and no tasks is accepted when asking is allowed,
     its questions returned (at most three, each checked like `ask_human`'s); with answers given, the prompt carries
     them, says not to ask again, and a reply's questions are ignored (no tasks still fails).
   - `ProjectService.AnalyzeProjectAsync` (a test seam on `WyvernFactory` for the analyzer's model): questions become
     project-level decisions (empty task id, `AskHuman`), a card is raised, no task file is written, the project stays
     `WyrmAssigned`; after they are answered, the next analysis gets the answers and creates the tasks.
   - `WyvernProcessingService`'s pick-up skips a project with an open project-level decision.
   - A restart: a new `ProjectService` over the same database still waits, then resumes with the answers.
   - `DecisionDesk` says the analysis will resume; Wyrm still has no `ask_human`.
2. **Model and prompt.** `WyvernAnalysis.Questions`; `Wyvern.AnalyzeProjectAsync` takes the answers and whether it may
   ask. The analysis request explains the optional `questions` field only when it may ask, and lists the answers
   (`HumanAnswer` text) when it has them. `ask_human`'s argument checks move to one shared place.
3. **The wait.** `DrakeFactory` (which owns the decision store, the ask policy and the card callback) gains
   `AskProjectQuestionsAsync` and `ProjectQuestionsAsync(projectId, since)`. A round counts from the project's last
   finished analysis (`AnalyzedAt`), so a changed specification starts a new one. `ProjectService` asks when the reply
   carries questions and no round was answered yet; `WyvernProcessingService` skips a project with an open round.
4. **Answering.** No Drake holds an empty task id, so `DrakeFactory`'s store fallback resolves it as it is;
   `DecisionDesk` and the card wording name the analysis. Drake's reconcile and expiry ignore project-level decisions.
5. Full suites, then the human test on a throwaway server; CLAUDE.md's Wyvern bullet.

## Notes

- **Red, after the fact.** The code was written before the tests, not test-first. To show the tests can fail, the
  analysis was run with asking switched off (`mayAsk` forced to false): 6 of `WyvernQuestionsTests`' 8 failed (the no-store
  test and the Wyrm test pass either way), and all pass with the change. Full .NET suite 661 passed; no client change.
- **Restart:** the open question lives in `pending_decisions`, which is what holds the project, so a new server over
  the same database still waits and then plans with an answer given after the restart (tested).
- **Rounds:** one per analysis, counted from the project's last finished analysis (`AnalyzedAt`), so a changed
  specification may ask again. A reply's questions are dropped when it may not ask; a question that fails
  `ask_human`'s checks is dropped and logged; at most three are kept.
- **Human test** (throwaway server `projects193`, vLLM `lan2`): a new project `notes170` whose specification leaves
  storage open (SQLite or one JSON file per note), created over REST and approved in Dragon.
  - Wyvern asked one question, "Which storage backend should the notes use?", with three options and its reason; no
    task was planned. Its card reached Dragon (task id empty) and Needs you; a full cycle later the analysis had not
    run again.
  - Answered "2" in Dragon: stored as "One JSON file per note in a notes directory", the reply said the project's
    analysis will resume, and the card was retired.
  - The next cycle planned 6 tasks in 3 areas built on one JSON file per note ("owner-confirmed decision", "not
    SQLite"), asked nothing more, and Drake picked the project up.
- Wyvern's extra structure request (when a plan has no structure) is a second model call; the tests' scripted plan
  carries a structure.
