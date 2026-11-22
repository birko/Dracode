---
id: TASK-193
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-039]
pr: null
github-issue: null
jira-key: null
---

# Usage records and rate limits name the provider class, not the configured provider

## Context

Found 2026-11-22 in TASK-192's human test. Two configured providers of one type and model (`finstat` and `lan2`, both
vLLM serving `finstat`) record their usage under the same provider, "vLLM (finstat)": `TrackedLlmProvider`
(`Birko.AI.Resilience/Services/TrackedLlmProvider.cs` ~203) writes `Provider: Name`, the LLM client's display name
(class and model), not the provider's configured name. Cost reports (`view_cost_report`, the Stats tab's cost by
provider) therefore cannot tell such providers apart, and a usage record cannot show which configured provider served
a call.

Likely the same cause: `ProviderRateLimiter` is consulted with that `Name` too (`CanMakeRequest(Name)`), so a limit
configured under a provider's configured name (`KoboldLair:RateLimiting:ProviderLimits[].Provider = "finstat"`) or its
type (`"vllm"`) never matches "vLLM (finstat)" and is never applied — seen in TASK-121's human test, where a 3-per-minute
limit had no effect.

`KoboldLairAgentFactory.CreateLlmProvider` (`DraCode.KoboldLair/Agents/AgentFactory.cs`) receives only the provider's
type; the configured name is known to its callers (`ProviderConfigurationService.GetProviderSettings*`).

## Acceptance criteria

- [ ] A usage record names the configured provider (e.g. `lan2`), so two providers of one type and model are told
      apart in cost reports; a test pins it, red today
- [ ] A rate limit configured under a provider's configured name applies to that provider's calls; a test pins it,
      red today
- [ ] Usage recorded before the change still reads (no migration needed, or a stated one)

## Out of scope

- Pricing per provider (`CostTracking.Pricing` keys) beyond what the name change requires

## Human test plan

- [ ] On a throwaway server with two providers of one type, use each from a Dragon chat: the cost report lists them
      separately

## Implementation plan

_Populated by `/tasks plan TASK-193` — leave empty until then._
