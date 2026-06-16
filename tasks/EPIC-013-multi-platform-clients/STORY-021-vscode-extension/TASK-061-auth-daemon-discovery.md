---
id: TASK-061
parent: STORY-021
feature: FEATURE-024
status: todo
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-059, TASK-033]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# VSCode auth (vscode.authentication) + daemon discovery

## Context

Authentication via VSCode's `vscode.authentication` API for the GitHub IdP (STORY-017), and daemon discovery via the same `~/.koboldlair/daemon.json` lookup or `koboldlair.server` setting. Confirm the IdP scopes the built-in API needs during planning.

## Acceptance criteria

- [ ] `Sign In` command runs GitHub OAuth via `vscode.authentication`, obtains a DraCode-usable token
- [ ] Daemon discovery: read `~/.koboldlair/daemon.json` when `koboldlair.useLocalDaemon`; else use `koboldlair.server`
- [ ] `Connect to Server` command sets/validates the server target
- [ ] Remote-context fallback (SSH / devcontainer) documented — `--server`/setting path works when local daemon isn't reachable
- [ ] Tests where supported

## Out of scope

- Server-side OAuth (STORY-017)
- Panel content (TASK-060), diff (TASK-062)

## Human test plan

- [ ] Run `Sign In` → GitHub consent via VSCode's auth UI → authenticated; with a local daemon running, the extension auto-discovers it; set `koboldlair.server` to a remote URL → it connects there instead

## Implementation plan

_Populated by `/tasks plan TASK-061` — leave empty until then._
