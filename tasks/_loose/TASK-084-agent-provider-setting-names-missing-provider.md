---
id: TASK-084
parent: null
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: [FIELD-003]
pr: null
github-issue: null
jira-key: null
---

# A stored agent provider setting that names a missing provider breaks Dragon completely

## Context

Found 2026-10-03: the dev DB had `dragonProvider: zai` (a provider *type*), while the only Z.AI provider is *named* `pi-zai`.
`DragonService.CreateSessionAgents` threw `Provider 'zai' not found for agent type 'dragon'` and the WebSocket closed without a
message — the UI gets nothing it can show. It was fixed by hand for the sign-off (`PUT /api/v1/providers/settings/agent`), but the same
can happen after any provider rename/delete. The provider-name/type confusion was fixed for Kobolds in `Drake.SummonKoboldAsync` (TASK-040).

## Acceptance criteria

- [ ] Renaming or deleting a provider either updates or clears agent settings that point at it, or the API refuses with a clear error
- [ ] When a setting still names a missing provider, Dragon falls back to the default provider with a logged warning (or sends the client a readable error) instead of dropping the socket
- [ ] Tests cover the missing-provider case for dragon/wyrm/wyvern/kobold resolution

## Out of scope

- Z.AI endpoint/plan routing (TASK-086)

## Human test plan

N/A — resolution and fallback are unit-testable.

## Implementation plan

_Populated by `/tasks plan TASK-084` — leave empty until then._