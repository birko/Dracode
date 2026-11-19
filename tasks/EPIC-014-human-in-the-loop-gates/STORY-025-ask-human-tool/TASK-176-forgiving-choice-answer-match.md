---
id: TASK-176
parent: STORY-025
feature: FEATURE-028
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
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

- [ ] FEATURE-028 has a decision on how a typed answer is matched to an option (the `proposed` row filed with this
      task), settled through `/feature decide` before any code changes
- [ ] Once decided: tests show the decided matches resolve to the option's exact text (which is what gets stored), an
      ambiguous answer (matching two options) is still refused with the options listed, and an answer matching none is
      refused as today
- [ ] The full suite passes

## Out of scope

- Card buttons (TASK-160).
- Free-text questions, which store the answer verbatim.

## Human test plan

N/A — fully covered by automated tests: matching is a pure function of the answer and the options.

## Implementation plan

_Populated by `/tasks plan TASK-176` — leave empty until then._
