---
id: TASK-165
parent: STORY-028
feature: FEATURE-031
status: todo
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-025]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# LLM judge for prose acceptance criteria, ahead of the human check

## Context

Found while planning TASK-025 (2026-11-16). TASK-025's acceptance check judges a finished task's target files and
declared API signatures by text match, and sends every prose criterion in the task description (Wyvern's
"Must implement:" items and verbatim criteria) to a human through EPIC-014's answerable notifications. Every task with prose criteria
would then wait for a person. A single LLM call can judge many of those criteria first, so only the ones it cannot
decide reach a human.

Builds on TASK-025's `TaskAcceptanceService` (its prose-criteria bucket) in `DraCode.KoboldLair/Services/`. If
EPIC-011's evaluator has landed by then, reuse its single-call client instead of adding another.

## Acceptance criteria

- [ ] Behind `KoboldLair:Verification:LlmJudgeEnabled` (default false); with it off, behaviour is TASK-025's unchanged
- [ ] With it on, each prose criterion gets one judgement — pass, fail or unknown, with a reason — from one LLM call per task
- [ ] Pass counts as met; fail counts as unmet, with the judge's reason in the fix or reopen text; only unknown reaches the human check
- [ ] The judge's provider and model resolve like other agents' settings; its cost is recorded by cost tracking
- [ ] Tests with a stubbed judge cover pass, fail, unknown and a judge error (an error counts as unknown, never pass)

## Out of scope

- The deterministic file and signature checks, and the human check itself (TASK-025)
- Per-action evaluation inside the tool loop (EPIC-011)

## Human test plan

- [ ] With the flag on and a real provider, finish a task whose spec says "errors are shown in red": the judge marks it pass or fail with a sensible reason, and an untestable criterion such as "feels fast" reaches the human check

## Implementation plan

_Populated by `/tasks plan TASK-165` — leave empty until then._
