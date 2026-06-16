---
id: FEATURE-024
created: 2026-05-31
---

# VSCode extension client — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | New `DraCode.KoboldLair.VsCode` TypeScript extension project | proposed | VSCode extensions are TypeScript/npm; kept separate from the .NET solution | — | — | — |
| D2 | Side-bar "KoboldLair" container with two panels: Chat (planning) and Runs (active + recent) | proposed | One home in the editor for conversation and run visibility | — | — | — |
| D3 | Register commands: Start Chat, Run on Selection, Connect to Server, Sign In | proposed | Core entry points, including running an agent on highlighted code | — | — | — |
| D4 | Discover a local daemon or use a configured server URL; settings `koboldlair.server` and `koboldlair.useLocalDaemon` | proposed | Works for local, remote, SSH, and devcontainer setups | — | — | — |
| D5 | Sign in via VSCode's built-in GitHub authentication API | proposed | Native, low-friction auth consistent with the rest of the client family | — | — | — |
| D6 | Present agent file changes as inline VSCode diff editors against the worktree state | proposed | Lets users review changes in place before accepting | — | — | — |
| D7 | Distribute via the VSCode Marketplace plus a `.vsix` attached to GitHub releases | proposed | Standard discovery channel with a manual side-channel | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-021 behaviour (panels, commands, daemon discovery, auth, inline diff, distribution).
