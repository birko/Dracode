---
id: TASK-105
parent: null
feature: null
status: todo
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

- [ ] Every agent that calls an LLM — Dragon and its council, Wyrm (pre-analysis and delegation), Wyvern, the Kobold
      planner, Drake if it calls one — records its calls through the cost tracker and goes through the rate limiter
- [ ] Each record carries its agent type (and project id where the agent has one), so the report can tell them apart
- [ ] Tests prove each of those construction paths produces a tracked provider (fails without the fix)
- [ ] Live: a Dragon conversation followed by `view_cost_report` shows today's Dragon requests

## Out of scope

- Configuring prices (`CostTracking.Pricing`) — a settings decision
- What the report shows or how it formats it (`ViewCostReportTool`)

## Human test plan

- [ ] On the dev server, chat with Dragon, then ask for the cost report summary → today's requests include the dragon/council calls

## Implementation plan
