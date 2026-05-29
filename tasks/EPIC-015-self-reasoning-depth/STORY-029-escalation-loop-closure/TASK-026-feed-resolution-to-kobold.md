---
id: TASK-026
parent: STORY-029
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

# Feed escalation resolution back into Kobold context

## Context

`Drake.HandleEscalationAsync` (`Drake.cs:765-916`) resolves escalations (revise plan / refine task / reset) but the Kobold resumes with no record of what changed or why — the loop is open, so the Kobold can repeat the rejected approach. Add a resolution payload Drake writes back, injected into the Kobold's context on resume. Reuses the synthetic-message resume channel from TASK-022 if EPIC-014 has landed; otherwise implements the minimal injection independently.

## Acceptance criteria

- [ ] Each auto-resolve path produces a structured resolution payload: disposition taken, what changed (plan diff / task refinement / reassignment), and explicit guidance ("do not retry X because Y").
- [ ] On resume, the payload is injected as a synthetic message before the next LLM call.
- [ ] Covers `WrongApproach` (revise), `NeedsSplit`/`TaskInfeasible`/`MissingDependency` (refine), `WrongAgentType` (reset → brief the *new* agent on the prior attempt).
- [ ] When a human resolved a parked decision (EPIC-014), the human's answer is carried as the resolution payload through the same channel.
- [ ] Feature-flagged; default off until measured.
- [ ] Test: a Kobold that escalated `WrongApproach` and got a revised plan references the revision in its next reflection instead of re-attempting the rejected approach.

## Out of scope

- The independent evaluator — EPIC-011.
- The resume/injection plumbing itself if EPIC-014/TASK-022 owns it — this task supplies the *content*, reuses the channel.

## Human test plan

- [ ] Induce a `WrongApproach` escalation, let Drake revise, and confirm the resumed Kobold's next reflection acknowledges the revision rather than repeating the rejected step.
- [ ] Induce a `WrongAgentType` reset and confirm the replacement agent is briefed on what the previous agent tried.

## Implementation plan

_Populated by `/tasks plan TASK-026` — leave empty until then._
