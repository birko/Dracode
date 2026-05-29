---
id: STORY-031
parent: EPIC-015
status: planned
created: 2026-05-29
---

# Cross-step coherence re-plan check

## User story

As a Kobold whose step N just failed or was revised, I want the remaining steps re-checked for coherence, so that step N+1 isn't executed against assumptions that step N just invalidated.

## Behaviour

- Today the plan is created once and locked; remaining steps are only revisited on a full escalation → `RevisePlanAsync`. A step that completes *differently* than planned (auto-advance, partial result, revised approach) leaves downstream steps stale.
- Add a lightweight re-plan check triggered when a step fails or is revised: assess whether the remaining steps' assumptions (target files, dependencies, prior-step outputs) still hold.
- If they still hold → continue unchanged (cheap, common case). If a contradiction is detected → revise only the affected downstream steps (reuse `RevisePlanAsync` machinery, preserving completed steps), or escalate if the revision is large.
- Must not become a full re-plan on every step — it's a cheap coherence gate, not a re-planning loop. Default to "continue" unless a concrete contradiction is found.
- Complements step-dependency ordering (analysed once at plan creation, `KoboldPlannerAgent.cs:217`) by re-validating mid-execution.
- Feature-flagged; default off until measured for thrash.
- Edge case: avoid oscillation — a step revised by the coherence check should not immediately re-trigger another coherence revision of the same steps (debounce / one revision per step transition).
