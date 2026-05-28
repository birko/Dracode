---
id: STORY-021
parent: EPIC-013
status: planned
created: 2026-05-28
---

# VSCode extension client

## User story

As a VSCode user, I want to drive KoboldLair without leaving the editor — chat with Dragon in a side panel, see Kobold runs inline as diffs against my workspace files.

## Behaviour

- New folder `DraCode.KoboldLair.VsCode/` (separate npm project; TypeScript)
- Extension manifest registers:
  - Side-bar view container "KoboldLair" with two panels: "Chat" (Dragon) and "Runs" (active + recent Kobolds)
  - Commands: `KoboldLair: Start Chat`, `KoboldLair: Run on Selection`, `KoboldLair: Connect to Server`, `KoboldLair: Sign In`
  - Settings: `koboldlair.server` (URL), `koboldlair.useLocalDaemon` (bool)
- Daemon discovery: same `~/.koboldlair/daemon.json` lookup, or `--server` URL from settings
- Authentication: same OAuth flow as the CLI, but via VSCode's `vscode.authentication` API for the GitHub IdP
- Inline diff view: when a Kobold run touches workspace files, present them as a VSCode diff editor (vs the worktree state)
- "Run on Selection" command: highlight code → invoke ad-hoc Kobold with selection as the prompt context

## Distribution

- Published to VSCode Marketplace under publisher `dracode` (placeholder; pick real publisher id during planning)
- Side-channel: `.vsix` attached to GitHub releases

## Risks

- Discoverability of the local daemon from inside VSCode: VSCode runs as a different user context sometimes (remote SSH, devcontainer). Document the `--server` fallback prominently.
- VSCode extension authentication friction: GitHub OAuth via the built-in API needs the right scopes — confirm during planning what scopes our IdP setup requires.
