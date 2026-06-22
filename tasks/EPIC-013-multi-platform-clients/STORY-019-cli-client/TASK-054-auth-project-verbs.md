---
id: TASK-054
parent: STORY-019
feature: FEATURE-022
status: blocked
priority: P1
assignee: ai
created: 2026-06-11
depends-on: [TASK-051, TASK-033, TASK-035]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# CLI auth + management verbs: login / keys / projects / status / stop

## Context

Auth and management verbs against a (possibly remote) daemon. `login` runs the GitHub device-code flow (STORY-017) and caches the token in `~/.koboldlair/auth.json`; `keys` manages service-account JWTs (TASK-035); `projects` is CRUD shortcut; `status`/`stop` operate the daemon.

## Acceptance criteria

- [ ] `koboldlair login` runs the device-code OAuth flow, caches token to `~/.koboldlair/auth.json`
- [ ] `koboldlair keys [create|list|revoke]` manages service-account JWTs on a remote daemon
- [ ] `koboldlair projects [list|create|delete]` performs project CRUD (REST or daemon)
- [ ] `koboldlair status` shows daemon state + active agents + active runs; `koboldlair stop` stops the local daemon
- [ ] Cached token auto-attached to `--server` calls; expired token triggers re-login prompt
- [ ] Tests: token cache read/write; verb arg parsing

## Out of scope

- Run/interactive verbs (TASK-052/053)
- Server-side OAuth/keys implementation (STORY-017 tasks)

## Human test plan

- [ ] `koboldlair login` against a remote daemon → device-code prompt → approve → `~/.koboldlair/auth.json` written; subsequent `koboldlair --server <url> projects list` succeeds without re-auth

## Implementation plan

_Populated by `/tasks plan TASK-054` — leave empty until then._
