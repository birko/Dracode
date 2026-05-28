---
id: STORY-009
parent: EPIC-010
status: planned
created: 2026-05-28
---

# WyrmAgent agent-type selection voting

## User story

As a KoboldLair maintainer, I want WyrmAgent to vote on agent-type selection when a task's primary technology is ambiguous, so that wrong specialist assignments (and the resulting `wrong_agent_type` escalations) drop.

## Behaviour

- **Lowest-cost voting target**: WyrmAgent makes a single tool call (`select_agent`) per task. Parallel runs are cheap.
- **Approach**: ONLY vote when confidence is uncertain. First run a single WyrmAgent call. If the model invokes `coding` (the generalist fallback) OR the task description has 2+ technology mentions, run 2 more in parallel and vote.
- **Decision rule**: simple majority. If still tied or split 3 ways, default to `coding` (generalist) so the task isn't blocked.
- **Cost**: 1-3× LLM calls per task at the Wyrm-delegation stage. Tasks with clear single-tech assignments stay at 1×.
- **Feature flag**: `KoboldLair:Voting:Wyrm:Enabled` (default `false`). Ambiguity threshold heuristic configurable.
- **Open question**: should the vote also consider the task's file extension hints (`.tsx` → react, etc.) as a "fourth vote"? Mixing heuristic + LLM votes is unusual but cheap.
