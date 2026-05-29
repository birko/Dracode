---
id: TASK-028
parent: STORY-031
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-05-29
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Lightweight re-plan check when a step is revised/failed

## Context

The Kobold plan is created once and locked; remaining steps are only revisited on a full escalation → `KoboldPlannerAgent.RevisePlanAsync`. A step that fails or completes differently than planned (auto-advance at `Kobold.cs:1414`, partial result) can leave downstream steps stale. Add a cheap coherence gate triggered on step failure/revision that checks whether the remaining steps' assumptions still hold, and revises only the affected ones if not.

## Acceptance criteria

- [ ] On step failure or revision, a coherence check assesses remaining steps' assumptions (target files, dependencies, prior-step outputs).
- [ ] Assumptions hold → continue unchanged (cheap, common path — must not re-plan on every step).
- [ ] Contradiction found → revise only affected downstream steps via existing `RevisePlanAsync` machinery (preserve completed steps), or escalate if the revision is large.
- [ ] Debounce: a step revised by the coherence check does not immediately re-trigger another coherence revision of the same steps (no oscillation).
- [ ] Complements (does not duplicate) the one-time dependency ordering at `KoboldPlannerAgent.cs:217`.
- [ ] Feature-flagged; default off until measured for thrash.
- [ ] Test: a step whose output invalidates a later step's assumption triggers a targeted revision of only that later step.

## Out of scope

- Full re-planning loops — explicitly avoided.
- Escalation routing changes — EPIC-014.

## Human test plan

- [ ] Construct a plan where step 2's actual output contradicts step 4's assumption; confirm step 4 is revised while steps 1-3 are untouched, and no oscillation occurs.
- [ ] Confirm a healthy plan (no contradictions) runs with the check on and incurs no extra revisions.

## Implementation plan

_Populated by `/tasks plan TASK-028` — leave empty until then._
