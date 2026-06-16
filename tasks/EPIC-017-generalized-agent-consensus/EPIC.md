---
id: EPIC-017
status: planned
created: 2026-05-29
owner: human
affects: []
---

# Generalized agent consensus

## Area of concern

KoboldLair has two **point-integration** approaches to cross-checking LLM decisions, tracked elsewhere:

- **EPIC-010 (voting)** — run *one* agent N times in parallel and agree (Wyvern breakdown, WyrmAgent selection, KoboldPlanner plan). Three bespoke, hardcoded integrations.
- **EPIC-011 (evaluator-optimizer)** — *one* separate critic agent (`EvaluatorAgent`) judges *one* worker's actions.

Both are valuable but narrow. Two gaps remain:

1. **No reusable mechanism.** Each EPIC-010 integration re-implements parallel-run + scoring + decision-rule by hand. There is no shared abstraction an arbitrary agent can call to say "get consensus on this decision."
2. **No diverse-panel consensus.** Neither pattern supports *multiple different specialist agents* agreeing on an output before it's accepted (e.g. `csharp` + `refactor` + `test` agents cross-checking a generated module). Voting is N identical runs; evaluator is a single dedicated critic. A heterogeneous panel catches failure modes that redundant or single-critic checks cannot.

This epic provides the **general consensus substrate** that EPIC-010's three point-integrations become consumers of, and that new high-stakes decisions can adopt without bespoke wiring. It builds on EPIC-016/STORY-033 (consensus verdicts persist as structured reasoning records).

Out of scope at the epic level:
- The specific EPIC-010 voting integrations and the EPIC-011 evaluator design — those stay in their own epics; this epic only generalizes the shared machinery and adds the diverse-panel mode.
- The human-in-the-loop gates (EPIC-014) — consensus is agent-to-agent; human gates are a separate escalation tier. They may compose (low consensus → escalate to human) but are tracked separately.

## Success criteria

- A single reusable consensus API exists; adding consensus to a new decision point requires configuration + a scoring/agreement rule, not a re-implementation of parallel orchestration.
- Both modes are supported: **homogeneous** (N runs of one agent — voting) and **heterogeneous** (a panel of distinct agent types).
- Every consensus run emits a structured verdict persisted as a reasoning record (EPIC-016/STORY-033), linked to the decision it judged.
- Every consensus integration is feature-flagged and has a documented cost multiplier (Nx LLM calls) and a deterministic test mode (all votes seeded equal).
- EPIC-010's three integrations are retrofitted onto the shared mechanism (or a migration path is documented if they ship first).

## Features

| Feature | Covers | Status |
|---------|--------|--------|
| [FEATURE-038](../../docs/features/FEATURE-038-reusable-consensus-mechanism/idea.md) | STORY-035 (reusable ConsensusService abstraction) | idea |
| [FEATURE-039](../../docs/features/FEATURE-039-diverse-panel-consensus/idea.md) | STORY-036 (diverse-panel heterogeneous consensus) | idea |
| [FEATURE-040](../../docs/features/FEATURE-040-retrofit-voting-integrations/idea.md) | STORY-037 (retrofit EPIC-010 voting integrations) | idea |
