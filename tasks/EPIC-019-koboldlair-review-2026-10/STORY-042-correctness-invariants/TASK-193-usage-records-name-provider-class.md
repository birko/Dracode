---
id: TASK-193
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [Birko.Framework/TASK-984]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-039]
pr: 63
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

- [x] A usage record names the configured provider (e.g. `lan2`), so two providers of one type and model are told
      apart in cost reports; a test pins it, red today
- [x] A rate limit configured under a provider's configured name applies to that provider's calls; a test pins it,
      red today — pinned through `ProviderName` on every path here; the limiter and pricing keying on it are pinned by
      Birko TASK-984's tests, and the human test saw the limit apply
- [x] Usage recorded before the change still reads (no migration needed, or a stated one)

## Out of scope

- Pricing per provider (`CostTracking.Pricing` keys) beyond what the name change requires

## Human test plan

- [x] On a throwaway server with two providers of one type, use each from a Dragon chat: the cost report lists them
      separately

## Implementation plan

Decided 2026-11-24 (the owner chose the Birko change): Birko TASK-984 adds `TrackedLlmProvider.ProviderName`, which
usage records, pricing and the rate limiter key on when set; DraCode passes its configured name.

1. Red first, `LlmUsageTrackingTests`: the test provider is named `lan2` (type `ollama`), and every tracked provider —
   Wyvern, Wyrm, Wyrm pre-analysis, the Kobold planner, a Kobold, Dragon — must carry `ProviderName = "lan2"`. Each
   `GetProviderSettings*` config carries the name; a config without one (or blank) leaves `ProviderName` null.
2. `ProviderConfigurationService.ConfiguredNameKey` (`koboldlair.providerName`) is added to every config the three
   `GetProviderSettings*` return.
3. `KoboldLairAgentFactory.Create` and `CreateLlmProvider` set `ProviderName` from that key.
4. CLAUDE.md "Cost Tracking" says what the limits and prices are keyed by.

## Notes

- **Birko side:** Birko TASK-984 (`birko-framework-44`, Birko.Framework main 16dc33f3, pushed). Without a configured
  name (a Kobold built with the factory's default config) the tracked provider keys on the client's name, as before.
- **No migration:** records made before keep "vLLM (finstat)" and still read; they no longer group with new ones.
  `RateLimiting.ProviderLimits[].Provider` and `CostTracking.Pricing[].Provider` must now use the configured name.
- **Tests:** 5 of the new tests red before the change; .NET suite 645 passed. No client change, so no e2e run.
- **Human test** (throwaway server, `projects193`, two vLLM providers on one model: `finstat` at $1/M, `lan2` at $10/M,
  2 requests a minute under `finstat` only):
  - Three back-to-back `lan2` messages took 0.7 s; through `finstat` the third waited 60 s ("Rate limit hit for
    finstat: 2/2 RPM").
  - Warden's cost report listed `lan2` (2 requests, $0.0992) and `finstat` (2 requests, $0.0107) as separate rows.
  - Birko TASK-984's own human test passed this way; the result was sent to `birko-framework-44`.
- **Found, not fixed:** Dragon's streamed replies through vLLM record no usage at all (the stream carries no token
  counts), so only its non-streamed and council calls are counted. Older than this task; not filed yet.
