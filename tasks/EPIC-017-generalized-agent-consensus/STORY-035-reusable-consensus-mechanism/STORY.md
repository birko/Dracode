---
id: STORY-035
parent: EPIC-017
status: planned
created: 2026-05-29
---

# Reusable ConsensusService abstraction

## User story

As a developer adding consensus to any decision point, I want a single reusable service so that I configure a scoring/agreement rule instead of re-implementing parallel orchestration, scoring, and decision logic each time.

## Behaviour

- A `ConsensusService` (or `ConsensusGate`) accepts: a set of candidate-producing delegates (1..N), a scoring function, an agreement/decision rule, and a config block (enabled flag, N, deterministic-mode seed).
- Returns a structured result: chosen candidate, the per-candidate scores, the agreement level reached, and a rationale — suitable for persisting as a reasoning record (EPIC-016/STORY-033).
- Supports the decision rules already described in EPIC-010: majority agreement, highest-score-among-agreeing, and synthesis-on-divergence (call again with all candidates as context).
- **Deterministic test mode**: when seeded, all candidate runs return the same answer so test fixtures stay stable (satisfies the EPIC-010 epic-level criterion centrally instead of per-integration).
- Feature-flag aware: when a consumer's flag is off, the service runs the single-candidate path with zero added cost.
- Edge case: a candidate delegate that throws is dropped from the pool, not fatal; if all fail, surface a clear error rather than a silent empty consensus.
