---
id: TASK-088
parent: null
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-03
depends-on: []
blocks: [TASK-086]
findings: [FIELD-005]
pr: null
github-issue: null
jira-key: null
---

# Dragon fails on a fresh server: "Provider 'zai' is not registered" (empty provider registry)

## Context

Found 2026-10-03 while working TASK-086. On a freshly started server the first Dragon session fails with
`ArgumentException: Provider 'zai' is not registered. Available providers:` (empty list) from
`LlmProviderFactory.Create`, via `KoboldLairAgentFactory.CreateLlmProvider` → `CreateBaseProvider`.
`AgentRegistration.RegisterAll()` (which also registers providers) ran only in `KoboldLairAgentFactory.Create`;
`DragonService` builds its provider through the public `CreateLlmProvider` and never goes through `Create`.
It only worked earlier in the day because an ad-hoc Kobold run had already registered everything in that
process. The code comment claimed `Create` was the chokepoint for every path — it was not.

## Acceptance criteria

- [x] `CreateBaseProvider` (the point both paths share) calls the idempotent `RegisterAll()`
- [x] On a freshly started server, the first Dragon session reaches the LLM (no registry error) — 2026-10-03: fresh server, first Dragon message answered and the project approved
- [x] Full test suite green — 180/180

## Out of scope

- Moving registration to a single startup hook — would be cleaner; not needed to fix the defect

## Human test plan

N/A — the live check is criterion 2 (a fresh server + one Dragon message). An automated test cannot show it red:
the registry is process-wide static state that any earlier test in the run already populates.

## Implementation plan

Not drafted — a one-line fix at the shared provider-creation point.