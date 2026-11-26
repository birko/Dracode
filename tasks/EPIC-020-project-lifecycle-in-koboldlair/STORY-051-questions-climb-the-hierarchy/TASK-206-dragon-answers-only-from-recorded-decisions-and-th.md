---
id: TASK-206
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
blocks: [TASK-207]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: 77
github-issue: null
jira-key: null
---

# Dragon answers only from recorded decisions and the specification, otherwise asks the person to define it

## Context

FEATURE-078 D19. Dragon is the last level before the person. It answers only from recorded decisions and the
specification's own text, never from its own judgement; when the specification does not cover something, it asks the
person to define it.

Decisions: FEATURE-078 D19.

## Acceptance criteria

- [x] Dragon answers a question a recorded decision or the specification's text settles, quoting it
- [x] A question neither settles reaches the person, asking them to define it
- [x] Dragon never answers from its own judgement

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] A question the specification does not cover reaches the person through Dragon as a request to define it

## Implementation plan

Planned at pick (2026-11-25):

1. The supervisor's answering (TASK-203) becomes `QuotedAnswerLevel` (repeat from the record, then a model answer kept
   only when its quote is in the sources and it is acceptable) and `QuotedAnswerAgent` (the role, sources and where a
   declined question goes are parameters); `SupervisorLevel` supplies its sources.
2. `DragonLevel`: the sources are the project's specification text and its answered decisions
   (`GetAnsweredForProjectAsync`); a question already answered in the project needs no model.
3. A level passing a question may leave the person a note (`IQuestionLevel.NoteForPerson`, collected into
   `ChainOutcome.PersonNote`); Dragon's asks them to define it, and Drake and the analyzer's questions append it to the
   question's context on the card.
4. The server registers the level with Dragon's model (`dragon-answer`).

## Notes

- **Red:** the note tests failed at first for a real reason — `DragonLevel` declared `NoteForPerson` without
  overriding anything, so the chain called the interface's default; the base now has a virtual one. The quote check is
  the shared base's, mutation-tested in TASK-203. 6 `DragonLevelTests`; full .NET suite 692 passed; no client change.
- **Human test** (throwaway server, vLLM `lan2`):
  - First project `greet206`: Dragon declined the maintainer question although the spec names the maintainer; asked
    directly, the model quoted the spec's own instruction that the worker "must ask two questions with the ask_human
    tool" — it read the spec as reserving the answer for a person. With neutral wording, asked directly, it answered
    "Jana Novakova" from "The maintainer is Jana Novakova." and declined the license.
  - Second project `greet206b` (neutral wording): the maintainer question was answered by **Drake**, since the planner
    had copied "expected answer: Jana Novakova" into the plan, so Dragon's answering was not exercised live (it is by the
    tests and the direct model call). The license question, which the spec does not cover, climbed Drake → Wyvern →
    Dragon → person, its card saying "The specification does not cover this, so your answer defines it."; answered
    "MIT" in Dragon, the task finished and `greet.py` starts with "# Maintainer: Jana Novakova" and "#
    SPDX-License-Identifier: MIT".
- The reason a level gives now reads "from the task's sources: …" ("the task's sources says" before).
