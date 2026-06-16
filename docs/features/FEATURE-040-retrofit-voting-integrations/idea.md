---
id: FEATURE-040
created: 2026-05-31
owner: human
status: idea
---

# Retrofit voting integrations onto the shared mechanism

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

Three existing cross-check features were each built with their own hand-rolled "run it several times and agree" logic. That means four divergent code paths doing essentially the same job, which is more to maintain, more places for bugs to hide, and inconsistent behaviour. Once a single shared consensus building block exists, these three should stop reinventing it.

## Proposed shape

Move the three existing voting integrations onto the shared consensus building block so there is one code path for consensus instead of four. Each integration keeps doing its own job — it supplies its own way of producing candidates, its own scoring, and its own decision rule — but the parallel running and the predictable test mode come from the shared service. This is a clean-up, not a behaviour change: the agreement rules, feature flags, and cost profile must stay exactly as they are today. Which path applies depends on timing: if the three integrations are built before the shared service, this is the migration that consolidates them afterwards; if the shared service lands first, the integrations are built directly on it and this becomes a verification that nothing bespoke remains. The work documents whichever path actually happened.

## Out of scope (initial)

- Changing the agreement rules, cost profile, or feature flags of the three integrations — behaviour must be preserved exactly.
- The shared consensus mechanism itself and the diverse-panel mode — tracked separately.
- The single-critic evaluator approach — out of scope.

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
