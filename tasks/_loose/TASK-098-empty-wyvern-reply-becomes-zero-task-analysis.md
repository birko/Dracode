---
id: TASK-098
parent: null
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-04
depends-on: []
blocks: [TASK-095]
findings: [FIELD-013]
pr: null
github-issue: null
jira-key: null
---

# An empty or unparseable Wyvern reply silently becomes a 0-task analysis and the project is marked Analyzed

## Context

Found 2026-10-04 during TASK-095's live check (project `drake-switch-check`, Z.AI `glm-5.3`). Two Wyvern runs — the first
analysis (57 s) and, after TASK-097's fix, a re-analysis (38 s) — both logged `Wyvern analysis completed … Tasks: 0, Areas: 0`
and moved the project to `Analyzed`. `analysis.json` has `projectName: ""`, no areas, empty `requirementsCoverage`; only
`constraints`/`outOfScope` are filled, and those are merged in from the Wyrm recommendation (`Wyvern.cs` "Gap 9 fix"), not
from the model's output.

`WyvernAgent.AnalyzeSpecificationAsync` (`WyvernAgent.cs:~170-180`) runs one iteration and, when the last assistant message
has no text, substitutes `"{}"`; `Wyvern.AnalyzeProjectAsync` (`Wyvern.cs:~632-640`) deserialises that into an empty
`WyvernAnalysis` without complaint. So an empty reply (a reasoning-only response, a truncated one, a provider hiccup) is
indistinguishable from "this spec needs no work". The raw reply could not be inspected: usage records are written only
for Kobold calls, and the reply is not logged.

## Acceptance criteria

- [x] An analysis with no areas/tasks for a spec that has deliverables is treated as a failed analysis (the project goes to `Failed` with an error message, or the analysis is retried) — never silently `Analyzed` with 0 tasks
- [x] The empty/unparseable reply case is logged with enough of the raw response (or its absence and finish reason) to diagnose it
- [ ] Root cause for the `drake-switch-check` runs identified (empty content vs. JSON shape mismatch) and fixed if it is in this codebase — ⚠ NOT MET — not reproducible (the 2026-10-06 run produced 2 tasks); split to TASK-101, which waits for the new log line
- [x] Regression test: an empty agent reply does not produce an `Analyzed` 0-task project; proven to fail before the fix
- [x] Live: a small project (two features) analysed on the dev server yields tasks — 2026-10-06: `drake-switch-check` re-analysis → Tasks: 2 (cli-1, doc-1)

## Out of scope

- Features never being passed to Wyvern — TASK-099
- Usage tracking for non-Kobold agents
- Deferred to TASK-101 — identifying the root cause once a captured raw reply exists

## Human test plan

- [x] On the dev server, approve a two-feature spec → Wyvern logs a non-zero task count and task files appear under `tasks/` — observed 2026-10-06 (Tasks: 2, cli-tasks/documentation-tasks files)

## Implementation plan

1. `WyvernAgent` keeps the raw reply (`LastRawResponse`) and no longer substitutes `{}` for an empty one — `ExtractJson` throws on empty.
2. `Wyvern.AnalyzeProjectAsync` treats an analysis whose areas hold no tasks as a failure and logs `Wyvern reply unusable … Raw reply, <n> chars: <excerpt>` (2000-char cap); the existing `ProjectService` catch marks the project `Failed` with "Analysis failed: …".
3. Regression tests with a canned `ILlmProvider`.
4. Live: re-run Wyvern on `drake-switch-check` and look for the new log line.

## Progress log

- 2026-10-06 — fix as in the plan. Live re-run on the combined build: Wyvern produced 2 tasks and the new warning did not fire, so the earlier empty reply did not recur — root cause split to TASK-101. `WyvernUnusableReplyTests` (4): empty, `{}` and a preamble object fail the analysis (all three red against the pre-fix code), a reply with tasks is accepted. Full suite: 199 passed.
