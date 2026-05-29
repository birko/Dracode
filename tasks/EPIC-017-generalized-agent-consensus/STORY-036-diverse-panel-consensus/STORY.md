---
id: STORY-036
parent: EPIC-017
status: planned
created: 2026-05-29
---

# Diverse-panel consensus (heterogeneous agents)

## User story

As a KoboldLair maintainer, I want a panel of *different* specialist agents to cross-check a high-stakes output before it's accepted, so that failure modes one agent type is blind to are caught by another.

## Behaviour

- Builds on STORY-035: the candidate/judge delegates are distinct agent types (e.g. for a generated C# module: `csharp` checks correctness, `refactor` checks design/duplication, `test` checks testability) rather than N runs of one agent.
- Each panellist returns a structured verdict (approve/reject + score + reason + concerns); the agreement rule aggregates across the *diverse* perspectives (e.g. accept only if no panellist rejects on its own dimension, or weighted majority).
- Distinct from voting (EPIC-010, redundant same-agent runs) and from the single EvaluatorAgent (EPIC-011) — this is a heterogeneous panel, each member prompted with a different lens.
- Applicable targets to evaluate (pick during planning, not all at once): final Kobold module output, escalation-resolution correctness, Wyvern breakdown soundness.
- Cost: N panellists = Nx calls but on *different* prompts; document the multiplier and gate behind a feature flag + priority threshold (e.g. only `critical` tasks).
- Each panel verdict persists as a `consensus-verdict` reasoning record (EPIC-016/STORY-033) capturing every panellist's individual view, not just the aggregate.
- Edge case: avoid deadlock/thrash — a bounded number of panel rounds, then fall through to human gate (EPIC-014) or accept-with-warning per policy.
