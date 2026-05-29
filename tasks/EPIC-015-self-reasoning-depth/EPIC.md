---
id: EPIC-015
status: planned
created: 2026-05-29
owner: human
affects: []
---

# Self-reasoning depth & end-to-end verification

## Area of concern

KoboldLair shipped a three-layer self-reasoning stack in 2026-03-15: prompt CHECKPOINTs, the `reflect` tool (`DraCode.KoboldLair/Agents/Tools/ReflectionTool.cs`), and the external `ReasoningMonitorService` (45s). EPIC-011 will add a separate critic model (the true "another" in evaluator-optimizer). This epic covers the **remaining reasoning gaps that an evaluator alone does not close** — places where reasoning is one-shot, lacks a closing feedback loop, or never verifies the whole against the original intent.

Concrete gaps from the 2026-05-29 review:
- **No end-to-end task verification.** A plan "completes" when every step is Completed/Skipped/Failed. `StepValidationService` (`Kobold.cs:1614`) only checks "file exists + non-empty." Nothing checks the *finished task* against the task's acceptance criteria (which Wyvern already writes into task descriptions).
- **No escalation loop closure.** Drake resolves an escalation but never tells the Kobold *what changed*; the Kobold resumes blind.
- **Reflection has no budget awareness.** `reflect` reports progress/confidence but not tokens/cost spent vs. remaining; the monitor's "budget exhaustion" is a crude >10-reflections heuristic, even though real cost data exists in `TrackedLlmProvider`.
- **No cross-step coherence.** When step N fails or is revised, step N+1's plan is not re-checked — the plan is locked except on full escalation.

Out of scope at the epic level:
- The independent evaluator model (EPIC-011) and voting/parallelization (EPIC-010).
- `GetDepthGuidance()` task-complexity tuning is captured as a stretch story but is lower priority than verification + loop closure.

## Success criteria

- Every task runs a final acceptance check against its acceptance criteria before being marked Done; failures create a fix task instead of silently passing.
- When Drake resolves an escalation, the resolution is fed back into the Kobold's context on resume.
- `reflect` and the monitor reason over real token/cost spend, not iteration counts.
- A revised/failed step triggers a lightweight re-plan check of the remaining steps.
- Each change is feature-flagged and measurable; no degradation of throughput on healthy tasks.
