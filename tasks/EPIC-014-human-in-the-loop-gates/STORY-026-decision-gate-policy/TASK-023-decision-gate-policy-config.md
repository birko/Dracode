---
id: TASK-023
parent: STORY-026
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-05-29
depends-on: [TASK-020]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# DecisionGatePolicy config + enforcement in escalation routing

## Context

TASK-020 implements the `HumanDecisionRequired` branch but with a test trigger. This task adds the per-project `DecisionGatePolicy` (in `projects.json` under the project, alongside `security`/`agents`) and makes `Drake.HandleEscalationAsync` consult it to decide auto-resolve vs. human gate. Defaults must preserve today's fully-autonomous behaviour.

## Acceptance criteria

- [ ] `DecisionGatePolicy` model + (de)serialization in the project config, following the existing config-layering precedence.
- [ ] Knobs: `criticalTasksRequireApproval`, `planRevisionApproval`, `taskRefinementApproval`, `humanDecisionConfidenceThreshold`, `allowAskHuman`, `maxQuestionsPerTask`, `decisionTimeoutMinutes`.
- [ ] `HandleEscalationAsync` consults the policy to choose between auto-resolve and `HumanDecisionRequired`.
- [ ] Empty/missing policy === current behaviour (regression test proving an escalation auto-resolves exactly as today).
- [ ] `criticalTasksRequireApproval: true` parks a Critical-priority task's escalation (test).
- [ ] `planRevisionApproval` / `taskRefinementApproval`: the proposed revision/refinement is surfaced for approval before being applied.
- [ ] Policy changes apply to new escalations only; already-parked tasks are unaffected.

## Out of scope

- Dragon UI for editing the policy (can be done via config/existing tools initially).
- Answering decisions in Dragon — TASK-024.

## Human test plan

- [ ] With default policy, run a task that escalates and confirm it auto-resolves (no behaviour change).
- [ ] Enable `criticalTasksRequireApproval`, run a Critical task that escalates, confirm it parks.
- [ ] Enable `planRevisionApproval`, confirm a revision waits for approval before swapping in.

## Implementation plan

_Populated by `/tasks plan TASK-023` — leave empty until then._
