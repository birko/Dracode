---
id: STORY-012
parent: EPIC-011
status: planned
created: 2026-05-28
---

# Integrate EvaluatorAgent into the Kobold tool loop

## User story

As a KoboldLair maintainer, I want the EvaluatorAgent to run after each meaningful Kobold action and influence the Kobold's next decision, so the agent has external feedback in its loop.

## Behaviour

- **Hook point**: after each tool call that touches the workspace or makes a commit. Skip for read-only tools (`list_files`, `read_file`) to keep cost down.
- **Flow**: Kobold makes tool call → result returned → EvaluatorAgent scores → if `reject`, the rejection + reason + `suggested_correction` are appended to the Kobold's context as a system message before its next iteration.
- **Abort condition**: 3 consecutive `reject` verdicts on the same plan step → trigger `wrong_approach` escalation (existing path in `HandleEscalationAsync`).
- **Cost**: roughly doubles per-task LLM volume. Mitigations:
  - Use a smaller/cheaper evaluator model (Haiku)
  - Skip evaluation for low-priority tasks
  - Configurable evaluation cadence (every N tool calls instead of every call)
- **Feature flag**: `KoboldLair:Evaluator:Enabled` (default `false`). Per-priority threshold configurable.
- **Risk**: evaluator can be wrong. Mitigate with the calibration measurements in STORY-013 — never enable in prod until the false-positive rate is documented.
- **Blocker**: STORY-011 must ship first (this story depends on the EvaluatorAgent existing).
