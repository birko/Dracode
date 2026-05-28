---
id: EPIC-011
status: planned
created: 2026-05-28
owner: human
affects: []
---

# Tight evaluator-optimizer loop for Kobold actions

## Area of concern

Anthropic's "Building Effective Agents" article describes the evaluator-optimizer pattern: *"one LLM generates a response while another provides evaluation and feedback in a loop."* The key word is **another** — a separate LLM, not self-evaluation.

KoboldLair's current "evaluator" is the Kobold's own `reflect` tool, called every 3 iterations. The Kobold reflects on itself — same model, same context, same blind spots. This is loose evaluator-optimizer at best.

A separate evaluator LLM would catch failures that self-reflection misses, including the class of bugs that motivated the silent-commit-failure fix (Kobold claims success, but the side-effects didn't actually happen). An external evaluator checking "did the commit actually happen against the expected_content?" would catch this from a different angle than the orchestration-layer fix already shipped.

Out of scope at the epic level:
- Evaluating Drake decisions (Drake is deterministic logic, not LLM-driven)
- Evaluating Wyvern's task breakdown (that's voting — EPIC-010/STORY-008 territory)

## Success criteria

- An `EvaluatorAgent` exists with a stable scoring rubric
- Integration is feature-flagged; can be disabled
- Measurable improvement on a historical-failed-tasks benchmark (STORY-013)
- Cost analysis: external evaluator adds at most Nx LLM cost per task; quality vs cost ratio documented
- Decision: keep, drop, or apply selectively (e.g., only for `critical`-priority tasks)
