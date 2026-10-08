---
id: STORY-040
parent: EPIC-018
status: planned
created: 2026-10-08
---

# Adopt the Birko KoboldLair upstreams (agent runtime, data, server plumbing)

## User story

As the KoboldLair engine, I want the agent loop, plan model, event / resilience stores, git tooling, WebSocket / SSE
plumbing and background-job hosting to come from Birko.Framework, so that DraCode keeps only its own agents,
orchestrators and domain — and stops carrying forks that have already drifted (the `Kobold.cs` loop copy drops the
cancellation token; `Birko.AI.Orchestration` is imported and unused).

## Source

The 2026-10-08 review of `DraCode.KoboldLair` and `DraCode.KoboldLair.Server` outside `Auth/`. Framework side: Birko
STORY-060 (TASK-527 – TASK-535) and STORY-061 (TASK-536 – TASK-542) in
`Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`. DraCode-side defects found in the same review are
in [[EPIC-019]] (TASK-118 – TASK-121, TASK-123). Two findings from it that are adoptions with no framework change
needed — TASK-122 (PathValidator) and TASK-124 (telemetry) — are filed here and are not blocked.

## Done when

Every task below has deleted its local copy, with the test suite green.
