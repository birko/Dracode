---
id: TASK-130
parent: STORY-040
feature: null
status: todo
priority: P2
assignee: ai
created: 2026-10-08
depends-on: []
blocks: []
blocked: 'waiting on Birko TASK-532 (Birko.Framework)'
pr: null
github-issue: null
jira-key: null
---

# Use the Birko config secret provider and field cipher; re-encrypt stored provider keys

## Context

`Security/ConfigSecretProvider.cs` (48) and `ProviderKeyCipher.cs` (55): key = SHA256(master), no key id, no associated data. Switching cipher changes the stored format.

Framework side: Birko TASK-532 in `Framework/Birko.Framework/tasks/EPIC-019-birko-backports-from-dracode`.

## Acceptance criteria

- [ ] Both local files deleted
- [ ] A one-off migration re-encrypts existing provider keys into the versioned format; old values readable until migrated
- [ ] Rotating the master secret is documented

## Out of scope

- The framework change itself (Birko TASK-532)

## Human test plan

N/A — covered by the existing tests unless a criterion says otherwise.

## Implementation plan
