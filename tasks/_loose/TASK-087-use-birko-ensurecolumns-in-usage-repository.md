---
id: TASK-087
parent: null
feature: null
status: in-progress
priority: P3
assignee: ai
created: 2026-10-03
depends-on: []
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Replace SqlUsageRepository's local add-missing-columns helper with Birko's EnsureColumns

## Context

TASK-082 fixed lost usage records with a local helper, `SqlUsageRepository.AddMissingColumnsAsync` (DetectDrift, then
`ALTER TABLE … ADD COLUMN … DEFAULT 0|''`). Birko TASK-510 adds the framework version:
`AbstractConnector.EnsureColumns(Type) : IReadOnlyList<ColumnDrift>`. It is **sync only**, back-fills existing rows with
`default(T)` in stored form, returns the closed drifts, and is never called by store init. It **throws**:
`NotSupportedException` (unmapped type / provider without catalogue) and `InvalidOperationException` before any DDL
(missing column that is primary, unique, identity, or a `[RequiredField]` string/byte[]). A missing table returns empty.

## Acceptance criteria

- [ ] `InitializeAsync` calls `_repository.Connector!.EnsureColumns(typeof(KoboldLairUsageRecord))` after `CreateSchemaAsync` and logs each closed drift; the local helper and its duplicate-column catch are removed
- [ ] Decide and state how a throw is handled at startup (let it fail startup, or log and continue with cost tracking degraded) — the repository is built at startup in Program.cs
- [ ] `SqlUsageRepositoryTests` stay green unchanged (old-shape upgrade, idempotent re-init, fresh DB); full suite green
- [ ] Doc comment points at Birko TASK-510 instead of carrying the workaround

## Out of scope

- Applying EnsureColumns to other DraCode repositories — only usage_records is known to drift (TASK-082's scan)

## Human test plan

N/A — the existing SqlUsageRepositoryTests cover the upgrade path against a real SQLite file.

## Implementation plan

_Populated by `/tasks plan TASK-087` — leave empty until then._