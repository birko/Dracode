---
id: TASK-192
parent: STORY-049
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-161]
blocks: []
findings: []
pr: 44
github-issue: null
jira-key: null
---

# Settings: a provider's models, the default provider, and which provider each agent uses

## Context

Split from TASK-161 (decided by the owner, 2026-11-22: "providers only, file the rest"). Settings → Providers lists,
adds, edits, validates and removes providers. `/api/v1/providers` also manages, for an admin with `manage_config`:

- each provider's switchable model list — `POST /providers/{name}/models`, `DELETE /providers/{name}/models/{modelId}`
  (the provider view already carries `models`);
- the default provider — `GET` / `PUT /providers/default`;
- which provider (and model) each orchestrator uses — `GET /providers/settings`, `PUT /providers/settings/agent`
  (dragon, wyvern, wyrm, kobold) — and the per-Kobold-type overrides, `PUT /providers/settings/kobold`.

None of these has a screen yet; the old view did them over `/wyvern`, which carries no caller identity (D8).

## Acceptance criteria

- [x] An admin sees and edits a provider's models (add, remove, enable) from the Providers tab
- [x] An admin sets the default provider, and which provider and model Dragon, Wyvern, Wyrm and the Kobolds use, with
      the per-Kobold-type overrides
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The providers themselves (TASK-161), API keys for service accounts (TASK-162), budgets (TASK-163)

## Human test plan

- [x] Set Dragon to use another provider and model, start a Dragon chat: the reply comes from that provider (its usage
      record names it)
  - **Passed 2026-11-22:** see Notes (the usage record names the provider class, not the configured provider)

## Implementation plan

1. Server: `GET /api/v1/providers/agent-types` (admin): the registered agent types a per-Kobold-type override can
   name, the orchestrators left out. Everything else exists.
2. Providers tab: a "Models" row action opens the provider's model list — add, enable/disable, remove.
3. A new Agents tab (`kobold-agent-providers`): the default provider; the provider and model of Dragon, Wyvern, Wyrm
   and the Kobolds; per-Kobold-type overrides (add, remove). The server's refusal is shown as it is.
4. Tests: .NET for the new endpoint; e2e for the models dialog and the tab.

## Notes

- **Server:** `GET /api/v1/providers/agent-types` (`manage_config`) lists `Birko.AI` `AgentFactory.GetRegisteredAgentTypes()`
  less dragon/wyvern/wyrm/kobold, sorted.
- **Client:** `kobold-providers-page` gains a "Models" row action and dialog (`POST|DELETE providers/{name}/models`;
  enabling or disabling re-posts the model). New `kobold-agent-providers` (Settings → Agents, `#/settings/agents`,
  shown when providers live in the database): three `b-form`s — default provider (`PUT providers/default`), the
  orchestrators (`PUT providers/settings/agent`, only for what changed), and Kobold overrides (`PUT
  providers/settings/kobold`; Remove sends a null provider) with a table. The server's `{ error }` is the error toast.
  **Found in the human test and fixed:** the tab loaded its providers once, so one added on the Providers tab could not
  be chosen — the tab now reloads each time it is opened; and a provider assigned but not available (deleted,
  disabled, or imported from older settings, like the `zai` this server's one-time import set for Dragon) showed as an
  empty choice — it is now listed as "<name> (not available)".
- **Tests:** `ProvidersEndpointsTests` +1 (the agent types; 403 for a viewer). e2e `settings.spec.ts` +7: the Models
  dialog (list, add, disable, remove), default provider and an orchestrator saved, a refused change shows the server's
  reason, overrides added and removed, `#/settings/agents`, a provider added on Providers choosable on Agents, an
  unavailable provider labelled; all red on the code before (the last two with their fix turned off). Suites .NET
  572/572, e2e 149/149.
- **Human test** (throwaway Testing server, scratch copy of `projects160` with its paths moved, a throwaway master
  key, an admin token, the LAN model): the one-time import had set Dragon to `zai`, which is not configured, so Dragon
  could not start ("No provider for agent type 'dragon'…"). Added provider `lan2` (the LAN model) and a model on it
  through the Models dialog; on the Agents tab Dragon showed `zai`, was set to `lan2` / `finstat` ("Dragon now uses
  lan2 (finstat)"); a new Dragon session then started and answered, its usage record `agentType dragon`. The record
  names the provider **class and model** ("vLLM (finstat)"), not the configured provider, so it cannot tell `lan2` from
  `finstat` (same class and model); that Dragon ran at all, with `lan2` its only assignment, is the evidence.
- **Seen while testing:** usage records (and rate limits) key on the provider class and model, not the configured
  provider → TASK-193.
