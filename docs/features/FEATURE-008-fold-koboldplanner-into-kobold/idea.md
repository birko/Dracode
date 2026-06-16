---
id: FEATURE-008
created: 2026-05-31
owner: human
status: idea
---

# Evaluate folding KoboldPlanner into Kobold

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Before a worker starts implementing a task, a separate planning agent produces a step-by-step plan, which the worker then executes. But the worker already pauses periodically to assess its own progress and decide whether to continue, change approach, or escalate — which is itself a form of planning. So we may be running an extra dedicated planning stage (and an extra AI call per task) whose main real value is persisting the plan as a saved artifact, rather than producing genuinely better thinking. It is worth confirming whether the separate planner earns its keep.

## Proposed shape

Compare today's "plan first, then execute" split against a worker-only approach where the worker is instructed to lay out its plan on its first step before doing anything else. Run both on the same tasks and compare total cost per task, plan quality (by manual review of samples), how reliably steps complete, how often work gets stuck, and — importantly — whether work can still resume cleanly after a restart. A key risk is that the dedicated planner today sees rich context (related plans, similar past tasks, established best practices); the worker-only approach must be fed the same context or its plans will degrade. The output is a decision: fold the planner in, keep it, or a hybrid.

## Out of scope (initial)

- The 23 specialized language workers (clearly differentiated by domain expertise)
- Resilience services (rate limiter, circuit breaker, cost tracker — infrastructure, not agent tiers)
- Plan-voting work, which depends on this story's outcome and must wait for it

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
