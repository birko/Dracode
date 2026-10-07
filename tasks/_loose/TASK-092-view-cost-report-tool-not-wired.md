---
id: TASK-092
parent: null
feature: null
status: verify
priority: P2
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: [FIELD-009]
pr: null
github-issue: null
jira-key: null
---

# The view_cost_report tool exists but no agent has it

## Context

Found 2026-10-03 (TASK-086). Asked to run `view_cost_report`, Dragon answered that no cost tool exists, and Warden
confirmed it. `ViewCostReportTool` (`DraCode.KoboldLair/Agents/Tools/ViewCostReportTool.cs`) is defined but never
instantiated anywhere, although CLAUDE.md lists it among the Dragon tools (added 2026-03-19). The usage data it would
report is now being saved again (TASK-082/087): 14 rows on 2026-10-03.

## Acceptance criteria

- [x] Find which council agent should own it (CLAUDE.md lists it with the Dragon tools; Warden owns progress/config tools) and wire it in with the `CostTrackingService` (and rate limiter when present) — Warden (it owns progress/config tools); `DragonService` builds it from the registered `CostTrackingService` + `ProviderRateLimiter`; Dragon's and `delegate_to_council`'s Warden descriptions now mention cost reports
- [x] A test proves the agent's tool list includes `view_cost_report` — `WardenCostReportToolTests` (proven to fail without the tool)
- [ ] Live: asking Dragon for the cost summary returns the usage rows (requests/tokens; cost stays 0 until `CostTracking.Pricing` is configured) — this is TASK-082's remaining criterion

## Out of scope

- Configuring prices (`CostTracking.Pricing` is empty — a settings decision, not this task)

## Human test plan

- [ ] Ask Dragon "show me the cost report summary" → it calls the tool and shows request/token totals for today

## Implementation plan

Drafted and done 2026-10-07.

1. `WardenAgent`: optional `ViewCostReportTool` parameter, added to its tools; prompt lists it under **Costs**.
2. `DragonService`: takes `CostTrackingService` + `ProviderRateLimiter` (optional), passes the tool to Warden; `Program.cs` resolves both.
3. Dragon's council description and `delegate_to_council` name cost reports as Warden's, so Dragon routes the question there.
4. Test: `WardenCostReportToolTests`.

## Close notes

- 2026-10-07: code complete, suite 235/235. Parked at `verify` on `task/TASK-092`: the live criterion (ask Dragon for the cost summary on a running server) has not been run.