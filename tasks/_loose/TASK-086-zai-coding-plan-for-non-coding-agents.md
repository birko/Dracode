---
id: TASK-086
parent: null
feature: null
status: done
priority: P2
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Z.AI: Dragon, Wyrm and Wyvern fail with "insufficient balance" and the model list is out of date

## Context

Found 2026-10-03 during the sign-offs. With the `pi-zai` provider, coding agents (Kobolds) work — `ZAiProvider` routes them to the
coding endpoint the plan covers — but Dragon (and so Wyrm/Wyvern) go to the general endpoint and get
`TooManyRequests: Insufficient balance or no resource package`, with the configured model and with `glm-5.3-flash` alike.
Without them no project can be specified, approved or analyzed, which blocks the live sign-off of TASK-038/040.
Z.AI also changed its models: calls report `glm-5.3-flash` while the DB lists glm-4.5 … glm-5.2.

## Acceptance criteria

- [x] Decide: route all agents to the coding endpoint when the account is on the coding plan (per-provider setting), or top up / use another provider for orchestrators — record the decision here — **decided 2026-10-03 (owner): all agents on the GLM Coding Plan.** No code change: the provider config value `useCodingEndpoint: true` on `pi-zai` (an explicit value wins over the per-agent-type routing in `CreateBaseProvider`)
- [x] Refresh the pi-zai model list to what Z.AI currently serves — the plan serves `glm-5.3` and `glm-5.3-flash` (older ids are auto-routed: 5.2/5.1 → 5.3, 4.7 → 5.3-flash). pi-zai now lists exactly those two, default `glm-5.3`; the seven old entries were removed. Applied on the dev DB through `/api/v1/providers` (key preserved)
- [ ] Dragon can complete a request with the chosen setup; then re-run TASK-038/040 live and unblock them — ⚠ NOT MET (second half) — split to TASK-038/040 (blocked reason updated). First half met, after fixing three defects found on the way (TASK-088 empty provider registry on a fresh server, TASK-089 provider name used as type, TASK-090 new projects start with agents disabled): two projects created on a running server were approved by Dragon, analyzed by Wyrm/Wyvern and executed by Drake/Kobold to a committed result. The re-run could not be done: project mode needs an unassigned task, and Drake takes a freshly analyzed task within a second; resetting a task over REST fails (TASK-091)
- [ ] Carried from TASK-082 (its criterion 4, second half): `view_cost_report` shows the usage rows recorded since 2026-10-03 — ⚠ NOT MET — split to TASK-092: Dragon and Warden report no cost tool; `ViewCostReportTool` is never instantiated. The data is there (14 rows, 52,310 tokens today)

## Out of scope

- Changing other providers

## Human test plan

- [x] Dragon answers a chat message and approves a throwaway project; Wyrm/Wyvern analyze it (needs the live account) — 2026-10-03, see criterion 3
- ➜ Moved to TASK-092 (2026-10-03): ask Dragon for `view_cost_report` (summary) → it lists the usage rows recorded since 2026-10-03 (requests/tokens; cost stays 0 until `CostTracking.Pricing` is configured)

## Implementation plan

_Populated by `/tasks plan TASK-086` — leave empty until then._