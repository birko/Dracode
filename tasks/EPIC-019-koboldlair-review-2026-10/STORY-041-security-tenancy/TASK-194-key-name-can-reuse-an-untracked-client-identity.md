---
id: TASK-194
parent: STORY-041
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
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
pr: 46
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

- [x] A key cannot be created with the name of any existing OAuth client, recorded in `service_keys` or not; a test
      registers a client the way an older server did (not recorded) and shows the name is refused, red today
- [x] Clients registered before `service_keys` existed get owner rows (backfill on startup, owner = unknown/admin), or
      the check reads the OAuth client store directly — say which: **the check reads the store directly**
- [x] Existing keys keep working

## Out of scope

- Renaming keys, key scopes or expiry

## Human test plan

N/A — the refusal is asserted by the test above.

## Implementation plan

1. `OAuthClientNames` (Server/Auth): whether any OAuth client has a name, any case. The SQLite client store refuses
   predicate reads, so it reads the store's own `oauth_clients` table (`OAuthClientEntity`) from the database the
   stores were built with; the in-memory store answers the predicate itself.
2. `POST /api/v1/keys` refuses a name that `service_keys` or the client store holds (409), and fails closed (503, no
   key) when the check throws.
3. Test: register a client through `OAuthServer.ClientRegistration` only (as `/register` did before TASK-162), then a
   key of that name in another case gets 409.

## Notes

- **Read the store, not a backfill.** A backfill would have to invent an owner for each old client (unknown or an
  admin) just so the name check could see it; reading the store needs no guessed data and also covers a client
  registered any other way that skips `service_keys`. The old clients still list for nobody, as before.
- The read loads every client row and compares names in memory; the client table is small (one row per key or app).
- Test isolation found on the way: the OAuth stores take their database when the host is built, before a test's
  settings apply, so the keys tests' clients had been accumulating in the shared `projects-test/koboldlair-test.db`
  across runs. Harmless until this check, which read them as taken names. `KeysEndpointsTests` now registers the
  stores on its own per-test database. Production is unaffected: there build-time and runtime config are the same.
- Red without the fix (only the new test failed); 579/579 .NET green with it, repeated runs green; e2e 153 passed, the
  one failure the known flaky `auth.spec.ts` name test (TASK-189), green on rerun.
