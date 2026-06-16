---
id: TASK-006
parent: STORY-001
status: todo
priority: P1
assignee: ai
created: 2026-05-28
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
feature: FEATURE-003
---

# Team collaboration (RBAC + shared workspaces)

## Context

Multi-user team support. Roles via existing Birko.Security.Authorization (admin / user / viewer). Shared agent definitions and workspaces.

## Acceptance criteria

- [ ] `Team` entity: name, members[], owner
- [ ] Team-scoped Agent definitions (shared across team members)
- [ ] Team-scoped Workspaces (pairs with TASK-005)
- [ ] Permission gating: admin can manage members, user can use, viewer is read-only
- [ ] Tenant isolation — teams can't see each other's data
- [ ] UI: team selector, member management page
- [ ] xUnit tests on permission checks

## Out of scope

- SSO / SAML at team level (separate concern)
- Multi-team membership (one team per user in v1)

## Implementation plan

_Populated by `/tasks plan TASK-006` — leave empty until then._
