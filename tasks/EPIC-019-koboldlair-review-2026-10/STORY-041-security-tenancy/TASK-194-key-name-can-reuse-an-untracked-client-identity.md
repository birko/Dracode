---
id: TASK-194
parent: STORY-041
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-162]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-040]
pr: null
github-issue: null
jira-key: null
---

# A new API key can take the name, and so the identity, of a client registered before key owners were recorded

## Context

Found 2026-11-22 while closing TASK-162. A service-account key acts as `service:<name>` (FEATURE-019 D14), and
projects it creates are owned by that subject. TASK-162 lets any signed-in user create keys and refuses a name another
key already has — but it checks only `service_keys` (`ServiceKeyRegistry.NameTakenAsync`), the owner table TASK-162
added. Clients registered through `/register` **before** that table existed are not in it, and the OAuth client store
cannot be listed, so their names are invisible to the check.

So a user can create a key named like such an older client (say `discord-bot`); the new key's tokens carry
`sub = service:discord-bot`, and it sees and acts on that client's projects — a privilege escalation across owners.
Only servers that registered clients before TASK-162 are affected, and only for those clients' names.

## Acceptance criteria

- [ ] A key cannot be created with the name of any existing OAuth client, recorded in `service_keys` or not; a test
      registers a client the way an older server did (not recorded) and shows the name is refused, red today
- [ ] Clients registered before `service_keys` existed get owner rows (backfill on startup, owner = unknown/admin), or
      the check reads the OAuth client store directly — say which
- [ ] Existing keys keep working

## Out of scope

- Renaming keys, key scopes or expiry

## Human test plan

N/A — the refusal is asserted by the test above.

## Implementation plan

_Populated by `/tasks plan TASK-194` — leave empty until then._
