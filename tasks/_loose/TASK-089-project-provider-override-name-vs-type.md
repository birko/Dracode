---
id: TASK-089
parent: null
feature: null
status: done
priority: P1
assignee: ai
created: 2026-10-03
depends-on: []
blocks: [TASK-086]
findings: [FIELD-006]
pr: null
github-issue: null
jira-key: null
---

# Wyvern/Wyrm project provider overrides pass a provider name where a type is needed

## Context

Found 2026-10-03 while working TASK-086. After Dragon approved a project, `ProjectService.AssignWyvernAsync` failed:
`Provider 'pi-zai' is not registered` from `WyvernFactory.CreateWyvern` → `KoboldLairAgentFactory.Create`. A project's
per-agent provider override (`GetProjectProvider`) holds a provider **name** (`pi-zai`); `WyvernFactory` passed it on
as if it were the factory **type** (`zai`), and kept the *default* provider's config (key, base URL, endpoint
settings) while switching only the name. `KoboldLairAgentFactory.Create` translates names only for appsettings
providers, not DB-backed ones. Same defect class TASK-040 fixed for Kobolds in `Drake.SummonKoboldAsync`.
`DrakeFactory.CreateDrake` has the identical override pattern (latent: no caller passes `provider` today).

## Acceptance criteria

- [x] `WyvernFactory` resolves the Wyvern and Wyrm overrides via `GetProviderSettingsByName` (type + that provider's own config); model override still applied
- [x] `DrakeFactory` override does the same
- [x] Live: an approved project on the dev server is analyzed by Wyrm and Wyvern (status reaches Analyzed) — 2026-10-03: analyzed into 1 task, Drake ran it to Done (two projects)
- [x] Full test suite green — 180/180

## Out of scope

- Normalising stored overrides to types, or validating them on write (related: TASK-084)

## Human test plan

N/A — criterion 3 is the live check.

## Implementation plan

Not drafted — replace the name-as-type assignment at the three override sites.