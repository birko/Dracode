---
id: STORY-029
parent: EPIC-015
status: planned
created: 2026-05-29
---

# Escalation loop closure — feed resolution back to the Kobold

## User story

As a Kobold whose escalation was handled upstream, I want to know *what changed* when I resume, so that I don't repeat the same mistake blind to the resolution.

## Behaviour

- Today `Drake.HandleEscalationAsync` resolves an escalation (revise plan / refine task / reset) but the Kobold resumes with no record of *why* or *what* changed — the loop is open.
- Add a resolution payload that Drake writes back to the Kobold's context: what disposition was taken, what changed in the plan/task, and any guidance ("the approach was revised because X — do not retry Y").
- On resume, this is injected as a synthetic message so the Kobold's next reasoning step accounts for the resolution.
- Applies to all auto-resolved escalation types (`WrongApproach` → revise, `NeedsSplit`/`TaskInfeasible`/`MissingDependency` → refine, `WrongAgentType` → reset/reassign).
- Ties into EPIC-014: when a human resolves a parked decision, the human's answer is the resolution payload fed back the same way.
- Edge case: for `WrongAgentType` (task reset for a *different* agent), the resolution should brief the new agent on what the previous one tried and why it was wrong.
- Behaviour proof: a Kobold that escalated `WrongApproach`, got a revised plan, and resumes should reference the revision in its next reflection rather than re-attempting the rejected approach.
