---
id: STORY-030
parent: EPIC-015
status: planned
created: 2026-05-29
---

# Budget- and cost-aware reflection

## User story

As a Kobold deciding whether to continue or escalate, I want to reason over how much budget I've actually spent and have left, so that "should I keep going?" reflects real cost, not just an iteration count.

## Behaviour

- `reflect` (`ReflectionTool.cs`) currently reports `progress_percent` and `confidence_percent` but is blind to spend; the monitor's "budget exhaustion" is a crude `>10 reflections && <50% progress` heuristic in `ReasoningMonitorService`.
- Real token/cost data already exists via `TrackedLlmProvider` (cost tracking, 2026-03-19) — feed per-task spent tokens / estimated cost and remaining budget into the reflection context and the monitor.
- `reflect` gains budget awareness: a decision can consider "I'm at 80% of this task's budget and 40% progress" → escalate `NeedsSplit` earlier and with evidence, instead of waiting for the iteration-count heuristic.
- The monitor's budget-exhaustion check is replaced/augmented with real spend vs. the project/task budget (`CostTracking.Budget`).
- Respect existing budget enforcement: this is reasoning input, not a second enforcement path.
- Feature-flagged; default off until measured.
- Edge case: when cost tracking is disabled (`CostTracking.Enabled: false`), reflection falls back gracefully to today's iteration-based behaviour.
