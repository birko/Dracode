---
id: STORY-010
parent: EPIC-010
status: planned
created: 2026-05-28
---

# KoboldPlanner plan voting

## User story

As a KoboldLair maintainer, I want KoboldPlanner to generate multiple plan candidates and pick the best, so that bad plans don't waste Kobold execution time.

## Behaviour

- **Mid-cost voting target**: KoboldPlanner runs once per task. Voting at this stage trades planning cost for execution savings.
- **Approach**: generate 2-3 plans in parallel via `CreatePlanAsync`. Score each on:
  - Step atomicity (heuristic: average step expects to touch ≤2 files)
  - Dependency cleanliness (steps in correct order per file write/read analysis)
  - Coverage of acceptance criteria (steps reference items from the task description)
  - Total step count (very high = over-decomposed; very low = under-decomposed)
- **Decision rule**: pick the highest-scoring plan. No fallback to synthesis (plans are complex enough that LLM synthesis is risky and can produce inconsistent dependencies).
- **Cost**: 2-3× LLM calls per task at the Planner stage. Tasks with `complexity: low` could skip voting (single call is fine for trivial plans).
- **Feature flag**: `KoboldLair:Voting:Planner:Enabled` (default `false`). Per-complexity threshold configurable.
- **Blocker**: if EPIC-009/STORY-006 decides to fold KoboldPlanner into Kobold, this story becomes "Kobold first-iteration plan voting" — re-scope or cancel accordingly. Wait on STORY-006's decision before starting.
