---
id: TASK-192
parent: STORY-049
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-161]
blocks: []
findings: []
pr: null
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

- [ ] An admin sees and edits a provider's models (add, remove, enable) from the Providers tab
- [ ] An admin sets the default provider, and which provider and model Dragon, Wyvern, Wyrm and the Kobolds use, with
      the per-Kobold-type overrides
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The providers themselves (TASK-161), API keys for service accounts (TASK-162), budgets (TASK-163)

## Human test plan

- [ ] Set Dragon to use another provider and model, start a Dragon chat: the reply comes from that provider (its usage
      record names it)

## Implementation plan

_Populated by `/tasks plan TASK-192` — leave empty until then._
