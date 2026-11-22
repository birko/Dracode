---
id: TASK-056
parent: STORY-023
feature: FEATURE-026
status: done
priority: P1
assignee: agent
created: 2026-06-11
depends-on: [TASK-033, TASK-145]
blocks: []
findings: []
pr: 23
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

- [x] When sign-in is required, `#/login` shows "Sign in with GitHub", which starts the server flow. No token-paste field remains anywhere
- [x] The callback's fragment lands in the auth store, the fragment is cleared from the URL, and the user arrives on the homepage
- [x] The token is refreshed before it expires. A 401 or an expired session returns to `#/login`
- [x] The header's user menu shows the signed-in user and Sign out. Sign out calls `/auth/logout` and clears the store
- [x] With `authRequired` false, the app opens without a sign-in screen
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it with a stubbed callback fragment, red before the change; the suite stays green

## Out of scope

- API keys (TASK-162)
- The server-side OAuth flow (STORY-017, done)

## Human test plan

- [x] Click "Sign in with GitHub" → complete consent → land on the homepage signed in (also TASK-033's live GitHub-federation check)
- [x] **Also verify TASK-034's deferred end-to-end scoping** (moved here when TASK-034 closed on 2026-10-03):
  - Sign in as user A and create a project.
  - Sign in as user B: B's project list omits A's project, and B's own creates carry B's `sub`.
  - An admin (`ViewAll`) sees both.
  - Under loopback-dev, creates are owned by `Guid.Empty` and the list shows all.
- [x] Sign out, then press Back: the app shows the sign-in screen, not a project

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **On Birko.Web.Shell's auth (TASK-524)**, nothing hand-written: `services/auth-session.ts` wires `readAuthFragment`
  (at startup, before the router reads the hash: the callback's `#access_token=…&refresh_token=…` goes into the store
  and leaves the address bar, so the homepage opens) and `createAuthSession` (refresh a minute before `exp` and after
  a 401, through `POST /auth/refresh`; a refused refresh clears the store and goes to `#/login`). The REST client takes
  its `onRefreshToken`; a 401 nothing fixes ends the session. Sign out posts the refresh token to `/auth/logout`
  (best effort) and clears the store.
- **Sign-in screen** (`login-view.ts`): "Sign in with GitHub", a link to the server's `/auth/github/login`; a signed-in
  user goes on to the homepage; with `authRequired` false it says sign-in is optional and links to the projects. No
  field takes a token.
- **User menu** (`app-shell.ts`): the signed-in user (the store's name, else the token's `name`/`sub`) with Settings
  and Sign out; nothing when signed out ("Guest" is gone; Profile dropped, there is no `#/profile`). The name is
  escaped: the shell's `renderUserDropdown` puts it into HTML as given, and a GitHub profile name is anyone's to set
  (reported to the Birko session).
- **A user's state goes with them:** signing out, or another user signing in, closes the Dragon socket and forgets its
  session and cards (a refreshed token for the same user keeps it); the notification store drops its list; the bell
  starts and stops with the sign-in.
- **The old token fields:** the legacy vanilla-JS pages in `wwwroot/archive` (token inputs in the old server selector)
  were still served at `/archive/…`, though the README said they were not; the client host now answers 404 there.
- **Tests:** e2e `auth.spec.ts`, 8 tests with a stubbed GitHub round trip (the server's login answered by a redirect
  back with tokens in the fragment): signed out → `#/login` with only the GitHub link; sign-in lands on the homepage
  with the fragment gone and the user shown; refresh before expiry; a 401 the refresh cannot fix → `#/login`; sign out
  revokes, clears, closes Dragon's socket, Back stays on sign-in; the name as text; the legacy pages 404; with sign-in
  off the app opens. 7 red on the old client (the sign-in-off one passes either way). Suite 94/94.
- **Human test, partly** (throwaway Testing server on a scratch copy, client host with `KoboldLair__AuthRequired=true`):
  signed out, `#/` went to `#/login` with the GitHub button (no inputs; light, and dark at 375px without sideways
  scroll); the button's target answered 404 because GitHub federation is not enabled on this machine; the legacy page
  404. With a server-issued token: the menu showed "Test Owner" with Settings and Sign out, the homepage that owner's
  two projects; Sign out cleared the store, closed Dragon's socket, and Back stayed on `#/login`. A token the server
  refused ended the session on `#/login`; so did an expired one. No console errors.
- **Human test with GitHub, 22 Nov** (the owner, on a throwaway Testing server with an empty scratch folder, the
  existing "DraCode" OAuth app with a client secret added and its callback set to
  `http://localhost:57087/auth/github/callback`, one allowed GitHub id): "Sign in with GitHub" came back signed in on
  the homepage with the owner's name (with diacritics) and no token in the address bar; GitHub showed no consent page,
  since the account had already authorized the app (for the Copilot provider), and the server logged the user created.
  The owner created `a-project` (owner `github:1064302`). A second user (a server-issued token for `github:999`, as
  the owner has one GitHub account) did not see it, got 404 opening it and only its own with `?scope=all`; its
  `b-project` is owned by `github:999`. With `DefaultRoles` = admin, signing in again listed both. Sign out landed on
  `#/login`, Back stayed there, and the server shows that session's refresh token revoked. Loopback bypass: a create
  without a token is owned by `00000000-0000-0000-0000-000000000000`; that caller lists only its own projects plainly
  and all of them with `?scope=all`, which is what the homepage asks for.