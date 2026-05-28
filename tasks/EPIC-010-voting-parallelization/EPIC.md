---
id: EPIC-010
status: planned
created: 2026-05-28
owner: human
affects: []
---

# Add voting parallelization to high-stakes decisions

## Area of concern

Anthropic's "Building Effective Agents" article describes **voting** as a key parallelization pattern: *"multiple attempts generating diverse outputs"* — particularly valuable when a single attempt can be subtly wrong but the errors are recoverable through agreement among parallel runs.

KoboldLair has **sectioning** (parallel Drakes via git worktrees, parallel Kobolds with `maxParallel`) but **no voting**. This means a single LLM call's quality determines the outcome at every decision point, even the high-stakes cascading ones.

This epic adds voting to the **3 most impactful decision points** — stages where one wrong answer cascades into wasted work or worse.

Out of scope at the epic level:
- Voting on Kobold code generation (high cost, low marginal value — code is verifiable by build/test, voting adds noise)
- Voting on Drake's task ordering (already deterministic by priority + dependencies)

## Success criteria

- Three voting integrations shipped (one per story), each behind a feature flag for A/B comparison
- Each integration documents: cost increase (Nx LLM calls), quality improvement (specific metric), recommended deployment threshold
- Each voting integration can be disabled via configuration without code changes
- A "deterministic mode" exists for tests (all parallel votes seeded to the same answer) so voting doesn't break test fixtures
