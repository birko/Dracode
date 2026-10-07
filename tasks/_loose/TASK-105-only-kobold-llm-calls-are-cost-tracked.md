---
id: TASK-105
parent: null
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-07
depends-on: []
blocks: []
findings: [FIELD-019]
pr: null
github-issue: null
jira-key: null
---

# Only Kobold LLM calls are cost-tracked — Dragon, the council, Wyrm, Wyvern and the planner are never recorded

## Context

Found 2026-10-07 during TASK-092's live check. Asked for the cost report, Dragon showed 0 requests for the day although
that very conversation had made several Dragon and Warden calls. `usage_records` holds 64 rows, all from Kobold agent
types (`coding` 9, `documentation` 12, `python` 43). The cost tracker and rate limiter are applied only where a provider is
wrapped in `TrackedLlmProvider`, which `KoboldFactory` does. Elsewhere the provider is built without them:
`DragonService.CreateSessionAgents` calls `KoboldLairAgentFactory.CreateLlmProvider(providerName, config)` with no tracker
(so Dragon and the Sage/Seeker/Sentinel/Warden council are never recorded), and `WyvernFactory` / `WyrmFactory` call
`KoboldLairAgentFactory.Create(...)` without a tracker. The planner and Drake paths need checking the same way. The
result: usage reports undercount, and budget limits and rate limits (`CostTracking.Budget`, `RateLimiting`) never apply
to these agents.

## Acceptance criteria

- [x] Every agent that calls an LLM — Dragon and its council, Wyrm (pre-analysis and delegation), Wyvern, the Kobold
      planner, Drake if it calls one — records its calls through the cost tracker and goes through the rate limiter — `DragonService` (one tracked provider per council member), `WyvernFactory`, `WyrmFactory` (both agents), `WyrmRunner.GetRecommendationAsync` via `WyrmService`, `DrakeFactory` (planner). Drake itself makes no LLM call
- [x] Each record carries its agent type (and project id where the agent has one), so the report can tell them apart — `KoboldLairAgentFactory.Create`/`CreateLlmProvider` take a `projectId`; Wyrm, Wyvern, the planner and Drake-summoned Kobolds pass theirs (Kobold rows had none before either)
- [x] Tests prove each of those construction paths produces a tracked provider (fails without the fix) — `LlmUsageTrackingTests` (Wyvern, Wyrm + pre-analysis, planner, Kobold; all four fail with the wiring reverted). Dragon's council is covered by the live check: its agents are built inside a WebSocket session
- [x] Live: a Dragon conversation followed by `view_cost_report` shows today's Dragon requests — 2026-10-07: the report showed 4 requests / 10,389 tokens for today; `usage_records` then held `dragon` 4 and `warden` 2 rows

## Out of scope

- Configuring prices (`CostTracking.Pricing`) — a settings decision
- What the report shows or how it formats it (`ViewCostReportTool`)

## Human test plan

- [x] On the dev server, chat with Dragon, then ask for the cost report summary → today's requests include the dragon/council calls

## Implementation plan

Done 2026-10-07.

1. `KoboldLairAgentFactory.Create` / `CreateLlmProvider`: optional `projectId`, set on the `TrackedLlmProvider`.
2. Trackers passed in by constructor (as `KoboldFactory` already was): `WyvernFactory`, `WyrmFactory`, `DrakeFactory`,
   `WyrmService` (→ `WyrmRunner.GetRecommendationAsync`); `DragonService` already had them (TASK-092). `Program.cs` wires them.
3. `DragonService.CreateSessionAgents`: one tracked provider per council member (`dragon`, `sage`, `seeker`, `sentinel`, `warden`).
4. `KoboldFactory.CreateKobold(…, projectId)`; Drake passes the task's project.
5. Read-only `Wyvern.AnalyzerProvider` and `Drake.PlannerProvider` so tests can see which provider those agents call.
6. Tests: `LlmUsageTrackingTests`.

## Close notes

- Closed 2026-10-07. Suite 239/239. Review (inline): no blockers. `WyrmRunner.RunAsync` / `RunMultipleAsync` still build
  untracked agents, but nothing calls them (only `GetRecommendationAsync` is used, by `WyrmService`).
- Ad-hoc `/kobold` runs have no project, so their Kobold rows carry none — correct.
