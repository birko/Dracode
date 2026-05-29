---
id: STORY-033
parent: EPIC-016
status: planned
created: 2026-05-29
---

# Persist structured decision & reasoning records

## User story

As an operator (and as downstream agents), I want every significant agent decision and reasoning/reflection step recorded as a structured row so that I can audit *why* the pipeline did what it did, learn from past runs, and let consensus checks read and write verdicts.

## Behaviour

- New DB entity (e.g. `ReasoningRecord` / `DecisionRecord`) captures: project id, task id, agent type + instance, timestamp, kind (decision | reflection | escalation | consensus-verdict), the reasoning text/structured payload, confidence, and links to the artifact it concerns (plan step, task, analysis).
- The existing `reflect` tool's output is persisted here instead of being ephemeral; escalation decisions and their routing are recorded.
- Records are queryable per project/task/agent and feed `SharedPlanningContextService` cross-project learning.
- Designed as the substrate for EPIC-010 / EPIC-011 / EPIC-017: a consensus, voting, or evaluator verdict is itself written back as a `consensus-verdict` reasoning record, linked to the decision it judged.
- Edge case: high-volume reasoning must not bloat hot-path latency — writes are async/batched where they aren't on a correctness path.
