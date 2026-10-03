---
id: TASK-086
parent: null
feature: null
status: todo
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

- [ ] Decide: route all agents to the coding endpoint when the account is on the coding plan (per-provider setting), or top up / use another provider for orchestrators — record the decision here
- [ ] Refresh the pi-zai model list to what Z.AI currently serves
- [ ] Dragon can complete a request with the chosen setup; then re-run TASK-038/040 live and unblock them

## Out of scope

- Changing other providers

## Human test plan

- [ ] Dragon answers a chat message and approves a throwaway project; Wyrm/Wyvern analyze it (needs the live account)

## Implementation plan

_Populated by `/tasks plan TASK-086` — leave empty until then._