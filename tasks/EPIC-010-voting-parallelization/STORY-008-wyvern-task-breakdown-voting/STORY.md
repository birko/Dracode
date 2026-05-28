---
id: STORY-008
parent: EPIC-010
status: planned
created: 2026-05-28
---

# Wyvern task breakdown voting

## User story

As a KoboldLair maintainer, I want Wyvern to vote on its task breakdown so that bad analyses don't cascade into wasted Kobold execution.

## Behaviour

- **Highest-impact voting target**: Wyvern's task breakdown is the most cascading stage. A wrong breakdown wastes ALL downstream work (Drake plans, Kobold execution, verification, commits).
- **Approach**: run `WyvernAgent.AnalyzeSpecificationAsync` 3× in parallel (cheap; same spec content for all 3). Score each output on:
  - Requirements coverage (fraction of spec requirements with at least one task)
  - Constraint propagation (fraction of constraints appearing in task descriptions)
  - Agent type validity (no invalid `agentType` values)
  - Task description quality heuristic (average length; presence of "Must implement:" / "Exports:" markers)
- **Decision rule**: if 2 of 3 outputs agree on key shape (same area split, same number of critical-priority tasks ±1, same constraint set), pick the highest-scoring of the 2. If all 3 diverge, escalate to a fourth call with all 3 outputs as context ("here are 3 candidate breakdowns; produce a synthesis").
- **Cost**: 3× LLM calls at the Wyvern stage. Wyvern runs once per project, so this is a fixed cost per project, not per task.
- **Feature flag**: `KoboldLair:Voting:Wyvern:Enabled` (default `false`). When disabled, behavior is unchanged.
- **Open question**: voting changes determinism of test fixtures. Need a "deterministic mode" that seeds all 3 votes to the same answer for test stability — coordinate with EPIC-010 epic-level criterion.
