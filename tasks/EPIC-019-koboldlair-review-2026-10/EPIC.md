---
id: EPIC-019
status: planned
created: 2026-10-08
owner: ai
affects: [DraCode.KoboldLair, DraCode.KoboldLair.Server, DraCode.KoboldLair.Client]
kind: review-intake
source: code-review — 2026-10-08 KoboldLair review (commit e0be2d7; framework side Birko EPIC-019 / STORY-060–061)
---

# KoboldLair review 2026-10

## Area of concern

The DraCode-side defects found by the 2026-10-08 review of `DraCode.KoboldLair`, `DraCode.KoboldLair.Server` and
`DraCode.KoboldLair.Client`. Five findings, first filed loose as TASK-118 – TASK-121 and TASK-123 and re-homed here so
`/fix-next` can drain them. Findings ids were backfilled on adoption (SEC-1, SEC-2, CR-1 – CR-3).

The same review also produced work that is not a defect — switching to a framework capability. That lives in
[[EPIC-018]] / [[STORY-040]] (TASK-122, TASK-124 and TASK-125 – TASK-140), not here.

## Success criteria

- Every task below is done, each with a regression test where its acceptance asks for one

## Stories

- [[STORY-041]] Security & tenancy — TASK-118, TASK-119
- [[STORY-042]] Correctness & invariants — TASK-120, TASK-121
- [[STORY-043]] Reuse & dead code — TASK-123
