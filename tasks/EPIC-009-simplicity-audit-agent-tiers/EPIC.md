---
id: EPIC-009
status: planned
created: 2026-05-28
owner: human
affects: []
---

# Simplicity audit — collapse over-stacked agent tiers

## Area of concern

KoboldLair currently runs **6 agent tiers** (Dragon → Wyrm → Wyvern → Drake → Kobold Planner → Kobold), **23 specialized Kobolds**, and **5 background services**. Anthropic's "Building Effective Agents" article advises: *"Start with a single optimized LLM call before reaching for multi-step agentic systems. Only add complexity when simpler solutions demonstrably underperform."*

This epic is a **research/spike** to identify tiers that don't earn their complexity. For each candidate collapse, the goal is an A/B test plan + concrete success criteria, not a blind merge. The risk of collapsing is losing useful separation; the risk of not collapsing is paying the LLM-call cost forever for marginal value.

Out of scope at the epic level:
- The 23 specialized Kobolds (language-specific agents are clearly differentiated by domain expertise)
- Resilience services (rate limiter, circuit breaker, cost tracker — infrastructure, not agent tiers)

## Success criteria

- One concrete decision per story: collapse / keep / hybrid, with measured evidence
- Each candidate has a documented A/B comparison method (pipeline quality, LLM cost, end-to-end latency, escalation rate)
- If a collapse is approved, a separate implementation epic is created with the migration plan
- If a tier is kept, the rationale is captured so the question doesn't recur every quarter

## Features

| Feature | Covers | Status |
|---------|--------|--------|
| [FEATURE-007](../../docs/features/FEATURE-007-merge-wyrm-wyvern/idea.md) | STORY-005 (merge Wyrm + Wyvern) | idea |
| [FEATURE-008](../../docs/features/FEATURE-008-fold-koboldplanner-into-kobold/idea.md) | STORY-006 (fold KoboldPlanner into Kobold) | idea |
| [FEATURE-009](../../docs/features/FEATURE-009-collapse-drake-services/idea.md) | STORY-007 (collapse Drake services) | idea |
