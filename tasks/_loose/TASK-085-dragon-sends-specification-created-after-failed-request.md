---
id: TASK-085
parent: null
feature: null
status: todo
priority: P3
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: [FIELD-004]
pr: null
github-issue: null
jira-key: null
---

# Dragon sends specification_created after a request that failed

## Context

Found 2026-10-03: when the LLM call failed (Z.AI insufficient balance), Dragon still emitted a `specification_created` frame for the
project named in the message, right after the error `dragon_message`. Nothing was created — the client would show a success notice
for a failure.

## Acceptance criteria

- [ ] Find what emits `specification_created` after the response and why it fires on a failed request
- [ ] A failed Dragon request emits no `specification_created` (test covers it)

## Out of scope

- The provider failure itself (TASK-086)

## Human test plan

N/A — covered by a DragonService test once the trigger is found.

## Implementation plan

_Populated by `/tasks plan TASK-085` — leave empty until then._