---
id: STORY-037
parent: EPIC-017
status: planned
created: 2026-05-29
---

# Retrofit EPIC-010 voting integrations onto the shared mechanism

## User story

As a maintainer, I want the three EPIC-010 voting integrations to consume the shared `ConsensusService` so that there is one code path for consensus, not four divergent ones.

## Behaviour

- Wyvern breakdown voting (STORY-008), WyrmAgent selection voting (STORY-009), and KoboldPlanner plan voting (STORY-010) are re-expressed as `ConsensusService` consumers: each supplies its candidate delegate, its scoring function, and its decision rule; the parallel orchestration + deterministic mode come from the shared service.
- Behaviour and feature flags from EPIC-010 are preserved exactly (no regression in agreement rules or cost profile); this is a refactor, not a behaviour change.
- Sequencing: if EPIC-010 stories ship *before* STORY-035, this story is the migration that consolidates them afterward; if STORY-035 ships first, EPIC-010 integrations are built directly on it and this story is a no-op verification. Document whichever path applies.
- Acceptance: no remaining bespoke parallel-vote-and-score loops outside `ConsensusService`; the deterministic test mode is exercised by each retrofitted integration's fixtures.
- **Depends on STORY-035.** Coordinate with EPIC-010/STORY-010's own blocker on EPIC-009/STORY-006 (KoboldPlanner fold decision).
