---
id: TASK-056
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P1
assignee: agent
created: 2026-06-11
depends-on: [TASK-033, TASK-145]
blocks: []
findings: []
pr: null
github-issue: null
jira-key: null
---

# Sign in with GitHub in the web UI

> Rewritten 2026-11-16 by `/feature decompose FEATURE-026`. It was "OAuth login button + service-account key
> management". The API keys moved to TASK-162, which builds the missing list endpoint (this settles the old
> "list has no server support" question). The served page now mounts the TypeScript shell (TASK-072), so the old
> "ships dark" warning no longer applies.

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces.
Users sign in with GitHub, and each sees their own projects; admins see all (D8).

**Build on the framework:** don't write the redirect parsing, JWT expiry check or refresh timer in the app. They
belong to Birko TASK-524 (`Birko.Web.Shell` auth). The token lives only in the Shell auth store (TASK-114), and the
`#/login` route exists as a placeholder (TASK-113).

The server side exists:
- `GET /auth/github/login` starts the flow.
- `GET /auth/github/callback` redirects to the configured post-login URI with `#access_token=…&refresh_token=…` in
  the fragment.
- `POST /auth/refresh` and `POST /auth/logout`.
- The client host's `/api/config` reports `authRequired`. While it is false, there is no sign-in, as today.

Decisions: FEATURE-026 D5, D8.

## Acceptance criteria

- [ ] When sign-in is required, `#/login` shows "Sign in with GitHub", which starts the server flow. No token-paste field remains anywhere
- [ ] The callback's fragment lands in the auth store, the fragment is cleared from the URL, and the user arrives on the homepage
- [ ] The token is refreshed before it expires. A 401 or an expired session returns to `#/login`
- [ ] The header's user menu shows the signed-in user and Sign out. Sign out calls `/auth/logout` and clears the store
- [ ] With `authRequired` false, the app opens without a sign-in screen
- [ ] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it with a stubbed callback fragment, red before the change; the suite stays green

## Out of scope

- API keys (TASK-162)
- The server-side OAuth flow (STORY-017, done)

## Human test plan

- [ ] Click "Sign in with GitHub" → complete consent → land on the homepage signed in (also TASK-033's live GitHub-federation check)
- [ ] **Also verify TASK-034's deferred end-to-end scoping** (moved here when TASK-034 closed on 2026-10-03):
  - Sign in as user A and create a project.
  - Sign in as user B: B's project list omits A's project, and B's own creates carry B's `sub`.
  - An admin (`ViewAll`) sees both.
  - Under loopback-dev, creates are owned by `Guid.Empty` and the list shows all.
- [ ] Sign out, then press Back: the app shows the sign-in screen, not a project

## Implementation plan

_Populated by `/tasks plan TASK-056` — leave empty until then._
