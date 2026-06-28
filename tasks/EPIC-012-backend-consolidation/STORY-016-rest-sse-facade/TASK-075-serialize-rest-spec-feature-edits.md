---
id: TASK-075
parent: STORY-016
feature: FEATURE-018
status: todo
priority: P2
assignee: ai
created: 2026-06-28
depends-on: [TASK-043]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Serialize concurrent REST spec/feature edits (lost-update guard)

## Context

The REST feature endpoints (TASK-043) do load-modify-write: `SpecificationService.LoadByNameAsync` →
`spec.WithFeatures(...)` → `PersistFeaturesAsync`. But `LoadByNameAsync` returns a **fresh**
`Specification` (with its own `_lock`) each call, so two concurrent `POST`/`DELETE /features` requests
read the same `specification.features.json`, mutate independent copies, and the later write wins — a
silent lost update. The Dragon tools don't have this problem because they share one cached
`Specification` instance per name and serialize on its lock; the stateless REST path has no
cross-request serialization. `Specification._lock` protects only the in-memory list, giving false
comfort. Surfaced by the TASK-043 code review.

## Acceptance criteria

- [ ] Concurrent feature add/delete on the same project no longer lose writes — serialize the
      read-modify-write per project (e.g. a per-project `SemaphoreSlim` in `SpecificationService`, or an
      optimistic version/ETag check on the features sidecar)
- [ ] The same guard covers `PUT /projects/{id}/specification` ↔ feature edits that both touch the
      version/hash
- [ ] Test: fire N concurrent feature POSTs at one project → all N features present afterward

## Out of scope

- Cross-process locking (single-server assumption holds for now)
- The Dragon-session path (already serialized via the shared cached instance)

## Human test plan

- [ ] N/A — covered by the concurrency test (no human-visible surface)

## Implementation plan

_Populated by `/tasks plan TASK-075` — leave empty until then._
