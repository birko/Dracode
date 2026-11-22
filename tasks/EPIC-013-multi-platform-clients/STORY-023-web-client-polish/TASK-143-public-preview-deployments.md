---
id: TASK-143
parent: STORY-023
feature: FEATURE-026
status: todo
priority: P3
assignee: ai
created: 2026-11-12
depends-on: []
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Public preview deployments of projects on subdomains

## Context

FEATURE-026 D23 (deferred, owner 2026-11-12): a project KoboldLair builds can be deployed as a public preview on its
own subdomain (e.g. `flappy-dragon.<host>`). The new workspace reserves a Preview link for it (D11). Kept out of the
UI rebuild because it needs its own decisions first.

## Acceptance criteria

- [ ] Grill the open questions before building: where previews run (which host, container per project or static
      hosting), DNS / wildcard certificate, what a project must provide to be deployable, who may publish (owner only?),
      how a preview is torn down, and isolation (untrusted generated code on a public subdomain)
- [ ] A project's workspace shows its preview URL and state once deployed

## Out of scope

- The web UI layout (FEATURE-026 D8–D22)

## Human test plan

- [ ] Deploy a finished sample project and open its subdomain from another machine

## Implementation plan
