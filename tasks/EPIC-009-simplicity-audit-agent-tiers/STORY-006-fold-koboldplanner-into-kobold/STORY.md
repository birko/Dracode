---
id: STORY-006
parent: EPIC-009
status: planned
created: 2026-05-28
---

# Evaluate folding KoboldPlanner into Kobold

## User story

As a KoboldLair maintainer, I want to know whether the `KoboldPlanner` agent can be folded into the `Kobold` itself, so I can decide if the separate planning stage is doing work that the autonomous Kobold loop (with `reflect`) doesn't already do.

## Behaviour

- Current flow: Drake → KoboldPlanner produces `plan.json` → Kobold executes plan step-by-step.
- Anthropic's autonomous-agent pattern is: "LLM uses tools in a loop based on environmental feedback." A Kobold with `reflect` already pauses every 3 iterations to assess progress + decide continue/pivot/escalate. That IS thinking before acting.
- **Hypothesis**: KoboldPlanner is paying for plan-as-artifact persistence (resumability across restarts), not for better thinking.
- **Test plan**:
  - Variant A: existing Plan-then-Execute split
  - Variant B: Kobold-only with explicit first-iteration instruction to emit its plan via `create_implementation_plan` before tool calls
  - Compare on: total LLM cost per task, plan quality (manual review of N samples), step-completion rate, escalation rate, resumability after intentional restart
- **Risk**: KoboldPlanner currently sees module API signatures, related plans, similar-task insights, best-practices — context-rich inputs. Variant B needs the same context injected somehow (system prompt? user message preamble?). Without it, Variant B's plans degrade.
- **Open question**: can `KoboldPlanner.RevisePlanAsync` be replaced by Kobold's own re-planning after escalation? Or is the external re-planner still valuable for non-Kobold escalations (e.g., from Drake)?
- **Interaction**: this story's outcome affects EPIC-010/STORY-010 (KoboldPlanner plan voting). Resolve this before starting that one.
