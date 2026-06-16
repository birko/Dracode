---
id: TASK-027
parent: STORY-030
feature: FEATURE-033
status: todo
priority: P2
assignee: ai
created: 2026-05-29
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Surface real token/cost spend into reflect tool & monitor

## Context

`ReflectionTool` reports progress/confidence but is blind to spend, and `ReasoningMonitorService`'s budget-exhaustion check is a crude `>10 reflections && <50% progress` heuristic. Real per-call token usage and cost already flow through `TrackedLlmProvider` and `CostTracking` (2026-03-19, `view_cost_report`). Feed real spent/remaining budget into reflection and the monitor so escalation decisions consider actual cost.

## Acceptance criteria

- [ ] Per-task spent tokens / estimated cost and remaining budget (project/task) are exposed to the Kobold's reflection context.
- [ ] `reflect` can factor budget into its decision (e.g. escalate `NeedsSplit` earlier with evidence "80% budget at 40% progress").
- [ ] `ReasoningMonitorService` budget-exhaustion check augmented/replaced with real spend vs. `CostTracking.Budget`, not iteration count.
- [ ] No second enforcement path — this is reasoning input; existing budget enforcement stays authoritative.
- [ ] Graceful fallback to today's iteration-based behaviour when `CostTracking.Enabled: false`.
- [ ] Feature-flagged; default off until measured.
- [ ] Test: with a near-exhausted task budget, the monitor flags budget pressure based on real cost, not reflection count.

## Out of scope

- Changing budget *enforcement* (blocking calls) — that logic already exists.
- New pricing/usage storage — reuse `SqlUsageRepository`.

## Human test plan

- [ ] Set a low project/task budget, run a task, and confirm reflection/monitor reference real spend and escalate earlier than the old iteration heuristic would.
- [ ] Disable cost tracking and confirm behaviour falls back cleanly.

## Implementation plan

_Populated by `/tasks plan TASK-027` — leave empty until then._
