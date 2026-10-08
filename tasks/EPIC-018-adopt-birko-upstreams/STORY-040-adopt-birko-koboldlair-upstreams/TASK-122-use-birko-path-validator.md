---
id: TASK-122
parent: STORY-040
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Replace ExternalPathValidator with Birko's PathValidator / PathHelper.IsPathSafe

## Context

`DraCode.KoboldLair/Validation/ExternalPathValidator.cs` (32) checks for traversal with `Contains("..")`. Birko.Helpers
already ships `PathValidator` and `PathHelper.IsPathSafe(…, allowedExternalPaths)`, which resolve the full path. The
string check misses absolute paths and symlinked escapes and rejects legitimate names like `a..b`.

Found in the 2026-10-08 KoboldLair review. No framework change needed.

## Acceptance criteria

- [ ] Callers use the Birko helpers; `ExternalPathValidator` deleted
- [ ] Tests: `..` traversal, absolute path outside the allowed roots, an allowed external path, a name containing `..`

## Human test plan

N/A — covered by tests.

## Implementation plan
