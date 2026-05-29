---
id: STORY-026
parent: EPIC-014
status: planned
created: 2026-05-29
---

# Policy-driven decision gates (per-project)

## User story

As a KoboldLair operator, I want to configure *which* decisions need a human and which auto-resolve, so that I can dial oversight up for critical work and leave low-risk work fully autonomous.

## Behaviour

- A per-project `DecisionGatePolicy` (stored in `projects.json` under the project, alongside `security` / `agents`), consulted by `Drake.HandleEscalationAsync` before it picks a disposition.
- Policy knobs (all default to today's auto-resolve behaviour so nothing changes unless opted in):
  - `criticalTasksRequireApproval` — escalations on `Critical`-priority tasks route to `HumanDecisionRequired`.
  - `planRevisionApproval` — `RevisePlanAsync` proposes a revision but a human approves it before it swaps in.
  - `taskRefinementApproval` — Wyvern's split/refine result is shown for approval before being applied.
  - `humanDecisionConfidenceThreshold` — reflect confidence below this parks for a human instead of auto-escalating to the same-model router (distinct from the existing auto-escalation `EscalationConfidenceThreshold`).
  - `maxQuestionsPerTask` / `allowAskHuman` — bound the `ask_human` tool (STORY-025).
  - `decisionTimeoutMinutes` — how long to wait before falling back to auto-resolve (STORY-024).
- Policy resolution mirrors the existing config-layering precedence; a missing/empty policy === current behaviour.
- Behaviour proof: with an empty policy, an escalation auto-resolves exactly as it does today; with `criticalTasksRequireApproval: true`, a Critical task's escalation parks.
- Edge case: policy changes mid-flight should apply to *new* escalations, not retroactively unpark already-parked tasks.
