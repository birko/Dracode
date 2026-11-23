---
id: TASK-176
parent: STORY-025
feature: FEATURE-028
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-17
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-021]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-026]
pr: null
github-issue: null
jira-key: null
---

# A typed answer that names an option is refused unless it repeats the option's whole text

## Context

Found 2026-11-17 in the same end-to-end run as TASK-175 (real throwaway server, `task/TASK-025` `c3d2c8f`). The Kobold
asked with the options `English (Hello, <name>!)` and `Slovak (Ahoj, <name>!)`, no free text. Answering `Slovak` over
`/dragon` returned `InvalidAnswer` ("is not one of the options … The question is still open"); only the full label
`Slovak (Ahoj, <name>!)` resolved it.

That is FEATURE-028 D5 as decided (choice answers are validated against the options), and a card with buttons
(TASK-160) never sends anything else. A person typing an answer does, though: in the Warden `pending_decisions` tool
or any text client, an option label is whatever the model wrote, often long, so an unambiguous short form is refused.
D5 did not decide how close an answer must be.

## Acceptance criteria

- [x] FEATURE-028 has a decision on how a typed answer is matched to an option (the `proposed` row filed with this
      task), settled through `/feature decide` before any code changes
- [x] Once decided: tests show the decided matches resolve to the option's exact text (which is what gets stored), an
      ambiguous answer (matching two options) is still refused with the options listed, and an answer matching none is
      refused as today
- [x] The full suite passes

## Out of scope

- Card buttons (TASK-160).
- Free-text questions, which store the answer verbatim.

## Human test plan

N/A — fully covered by automated tests: matching is a pure function of the answer and the options.

## Implementation plan

Decided first: FEATURE-028 D9, `changed` by the owner 2026-11-23 — an answer names an option by its text in any case,
the start of its text ending at a word, or its number; the same on a question that also allows free text.

1. `AskHumanQuestion.Match` (exact text → whole text in any case → number from 1 → start at a word; two at one step is
   ambiguous); `Accepts` stores the matched option's full text, and on a free-text question keeps anything else as typed.
2. `NumberedOptions()`: the refusal (`DecisionDesk`) and Warden's `pending_decisions` listing number the options; an
   ambiguous answer gets its own refusal.
3. Tests: `AskHumanToolTests` (matches, no match, ambiguous, a numeric label, free text) and a `DecisionDeskTests` case
   through Drake (ambiguous and unmatched refusals numbered, "2" stores the option's full text).

## Notes

- Red before the change: the new cases failed on exact-only matching (9 in `AskHumanToolTests`, the desk case on its
  message); one old case flipped by the decision ("sqlite" was refused, now resolves to "SQLite").
- A label that is itself a number is matched as text first, so "2" picks the option "2", not the second option.
- The options are now numbered where a person types (the refusal, Warden's list); the dock's cards keep their buttons.
- 596/596 .NET, 154/154 e2e. No human test, per the plan: matching is a pure function of the answer and the options.
