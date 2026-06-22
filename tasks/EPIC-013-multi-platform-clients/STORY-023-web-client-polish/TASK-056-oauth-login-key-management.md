---
id: TASK-056
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-033, TASK-035]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Web OAuth login button + service-account key management UI

## Context

Replace the existing token-paste field (`auth-store.ts`) with a GitHub OAuth login button integrating STORY-017's flow (TASK-033), and add a service-account key management UI under Settings → API Keys (mirrors CLI `koboldlair keys`, TASK-035).

## Acceptance criteria

- [ ] OAuth login button replaces the token-paste field; completes the GitHub flow and stores the session via `auth-store.ts`
- [ ] Logout clears the session; expired token routes back to login
- [ ] Settings → API Keys: create / list / revoke service-account keys (same operations as CLI)
- [ ] Newly created key secret shown once, with copy affordance
- [ ] Tests where supported; manual flow covered below

## Out of scope

- Server-side OAuth/keys (STORY-017 tasks)
- Daemon status / switcher (TASK-055)

## Human test plan

- [ ] Click "Sign in with GitHub" → complete consent → land authenticated; open Settings → API Keys → create a key, see it once, revoke it
- [ ] **Also verify TASK-034's deferred end-to-end scoping** (it's in `review` waiting on this UI): log in as user A, create a project; log in as user B → B's project list omits A's project, B's own creates carry B's `sub`; an admin (`ViewAll`) sees both; under loopback-dev, creates are owned by `Guid.Empty` and the list shows all. When this passes, close TASK-034 → `done`.

## Implementation plan

_Populated by `/tasks plan TASK-056` — leave empty until then._
