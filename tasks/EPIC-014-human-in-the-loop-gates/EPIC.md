---
id: EPIC-014
status: planned
created: 2026-05-29
owner: human
affects: []
---

# Human-in-the-loop decision gates

## Area of concern

KoboldLair is autonomous by design — Dragon is the only interactive touchpoint, and Wyrm / Wyvern / Drake / Kobold run in background services with no way to ask a human anything mid-execution. When a Kobold escalates (`WrongApproach`, `TaskInfeasible`, `NeedsSplit`, …), `Drake.HandleEscalationAsync` (`DraCode.KoboldLair/Orchestrators/Drake.cs:765-916`) **auto-resolves** it: it calls `KoboldPlannerAgent.RevisePlanAsync`, `Wyvern.RefineTaskAsync`, or resets the task — the same class of LLM judgment that produced the problem also fixes it. The human is told *after the fact* via fire-and-forget notifications (`ProjectNotificationService`) that they must poll with `view_notifications`.

This epic adds the missing capability: **let an automatic agent stop and route a genuine decision to a human when one is warranted**, without halting the whole project. The human becomes a routable escalation target alongside Planner and Wyvern — chosen by policy — rather than a passive recipient of notifications.

Anchored to the gaps surfaced in the 2026-05-29 review:
- `ask_user`/`AskUserTool` is wired only for the interactive Dragon CLI (`DraCode/Program.cs:508`); automatic agents have no equivalent.
- Escalations always auto-resolve downstream; there is no "park and wait for human" disposition.
- Notifications are informational only — nothing ever blocks on a human answer.

Out of scope at the epic level:
- Making the whole pipeline approval-gated by default (autonomy stays the default; gates are opt-in via policy).
- The independent critic model — that's EPIC-011 (evaluator-optimizer).
- Replacing `pause_project` / `resume_project` (those stay as the coarse project-wide control).

## Success criteria

- A task can enter an `AwaitingHumanDecision` state that blocks *only that task* while the project keeps running other tasks.
- Automatic agents (Kobold, Wyvern) can pose a structured question to a human via an async `ask_human` tool and have the answer routed back into their context.
- A per-project policy decides which decisions auto-resolve vs. require a human, with a safe default (auto-resolve, preserving today's behaviour).
- Dragon surfaces pending decisions as actionable prompts, not just readable notifications, and the answer round-trips to unpark the task.
- No regression: with the default policy, the pipeline behaves exactly as it does today.

## Features

| Feature | Covers | Status |
|---------|--------|--------|
| [FEATURE-027](../../docs/features/FEATURE-027-blocking-decision-tier/idea.md) | STORY-024 — TASK-019, TASK-020 | idea |
| [FEATURE-028](../../docs/features/FEATURE-028-ask-human-tool/idea.md) | STORY-025 — TASK-021, TASK-022 | idea |
| [FEATURE-029](../../docs/features/FEATURE-029-decision-gate-policy/idea.md) | STORY-026 — TASK-023 | idea |
| [FEATURE-030](../../docs/features/FEATURE-030-answerable-notifications/idea.md) | STORY-027 — TASK-024 | idea |
