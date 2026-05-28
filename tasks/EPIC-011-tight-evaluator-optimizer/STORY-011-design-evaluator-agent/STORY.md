---
id: STORY-011
parent: EPIC-011
status: planned
created: 2026-05-28
---

# Design EvaluatorAgent

## User story

As a KoboldLair maintainer, I want a separate `EvaluatorAgent` that scores Kobold actions against the plan step's `expected_content` so that I have a critic independent of the Kobold's self-reflection.

## Behaviour

- **Inputs**: the plan step being executed, the Kobold's last action (tool call + result), the workspace state diff since the action.
- **Output**: structured JSON `{ "verdict": "approve" | "reject", "score": 0-100, "reason": "<one paragraph>", "missing_content": ["<item from expected_content>"], "suggested_correction": "<optional>" }`.
- **Rubric** (in the SystemPrompt):
  - Does the action move toward at least one `expected_content` item? → `approve` if yes
  - Did the action introduce a clear regression (file deleted, syntax error introduced)? → `reject` regardless of progress
  - For commit actions: does the diff actually contain the `expected_content` items? → `reject` if not
- **Out of scope**: the evaluator does NOT execute tools or make changes. Read-only.
- **Mode**: the agent runs as a single LLM call per evaluation (no autonomous loop). Probably uses a cheaper/faster model than the Kobold (Haiku tier).
- **Risk**: a strict evaluator that rejects too aggressively causes thrash. A lenient evaluator catches nothing. Need a calibration story (covered by STORY-013).
- **Open question**: should EvaluatorAgent see the full conversation history or just the last action? More context = better judgment but higher token cost. Default to last action + plan step; revisit if it underperforms.
