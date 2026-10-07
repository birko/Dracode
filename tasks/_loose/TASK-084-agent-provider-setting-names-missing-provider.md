---
id: TASK-084
parent: null
feature: null
status: done
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

- [x] Renaming or deleting a provider either updates or clears agent settings that point at it, or the API refuses with a clear error — `ProviderConfigurationService.DeleteProviderAsync` clears every setting naming it; deleting the default is refused (REST 409). There is no rename: a rename is create + delete, so the same path covers it
- [x] When a setting still names a missing provider, Dragon falls back to the default provider with a logged warning (or sends the client a readable error) instead of dropping the socket — falls back to the default with a warning (all agents, not only Dragon); if the default is missing too, Dragon sends an `error` message and closes the socket cleanly
- [x] Tests cover the missing-provider case for dragon/wyrm/wyvern/kobold resolution — `MissingProviderSettingTests` (fallback cases proven to fail without the fallback)

## Out of scope

- Z.AI endpoint/plan routing (TASK-086)

## Human test plan

N/A — resolution and fallback are unit-testable.

## Implementation plan

Drafted and done 2026-10-07.

1. `ProviderConfigurationService.ResolveConfiguredProvider`: a setting naming no configured provider resolves to the
   default provider with a warning, and drops the setting's model (it belonged to the missing provider). Default missing
   too → `InvalidOperationException` naming both. Used by `GetProviderSettingsForAgent` and `GetProviderSettingsForKoboldAgentType`.
2. `ProviderConfigurationService.DeleteProviderAsync`: refuses the default provider; clears dragon/wyvern/wyrm/kobold and
   `kobold:*` settings naming it; deletes the row and reloads. REST `DELETE /providers/{name}` calls it (409 on refusal).
3. `DragonService`: if the session's agents cannot be created on connect, send an `error` (`errorType: configuration`)
   and close the socket instead of dropping it silently.
4. Tests: `MissingProviderSettingTests`.

## Close notes

- Closed 2026-10-07. Review gate (inline): no blockers. A *disabled* provider named by a setting is still used as before —
  only a missing one falls back. The Dragon connect-error path is not unit-tested (it needs a live socket with no usable
  default provider); the fallback means it is reached only when the default provider is missing too.
- Suite: 234/234.