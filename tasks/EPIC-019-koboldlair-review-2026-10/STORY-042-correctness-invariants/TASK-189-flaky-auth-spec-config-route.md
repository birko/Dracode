---
id: TASK-189
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-036]
pr: null
github-issue: null
jira-key: null
---

# An e2e auth test fails about one run in six: its config route is still fetching when the page closes

## Context

Found 2026-11-22 while closing TASK-169 (and seen as an unnamed "cold-start" failure on TASK-168 and TASK-186).
`DraCode.KoboldLair.Client/tests/ui-e2e/auth.spec.ts` › "the user name from the token is shown as text" fails about
1 run in 5–8, on a warm client host too, with:

```
Error: Target page, context or browser has been closed
Consider awaiting `await page.unrouteAll({ behavior: 'ignoreErrors' })`
> 25 |     const response = await route.fetch();
```

The spec's `serve()` stubs `**/api/config` with a handler that forwards the request (`route.fetch()`) and patches the
answer. That test asserts quickly and ends while a `/api/config` request is still being forwarded, so the handler
throws when the page closes, and Playwright fails the test. Other specs that use `serve()` can hit the same race.

## Acceptance criteria

- [ ] The `/api/config` handler in `auth.spec.ts` no longer fails a test when the page closes during its fetch (e.g.
      `page.unrouteAll({ behavior: 'ignoreErrors' })` at the end, or the handler tolerating a closed page)
- [ ] `npx playwright test auth.spec.ts --repeat-each=20` passes with no failure
- [ ] The full e2e suite stays green

## Out of scope

- The product's sign-in code (the failure is in the test's own stub)

## Human test plan

N/A — a test-harness fix, proven by the repeated run above.

## Implementation plan

_Populated by `/tasks plan TASK-189` — leave empty until then._
