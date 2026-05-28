---
id: STORY-019
parent: EPIC-013
status: planned
created: 2026-05-28
---

# DraCode.KoboldLair.Cli — single-file binary CLI client

## User story

As a developer, I want a standalone `koboldlair` binary that I can run from any directory and immediately drive Dragon chat, Wyvern analysis, or single Kobold runs — without opening a browser, without manually starting a server.

## Behaviour

### Project rename + scaffold

- Rename `DraCode/` → `DraCode.KoboldLair.Cli/`; update `DraCode.slnx`
- Spectre.Console for TUI (already a dependency)
- Single-file publish profiles for `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`

### Verbs

| Verb | Purpose |
|---|---|
| `koboldlair do <prompt> [--agent <type>] [--server <url>]` | Ad-hoc Kobold against cwd, worktree-always, spawns ephemeral server if no daemon |
| `koboldlair run <projectId>/<taskId>` | Project-scoped Kobold execution |
| `koboldlair chat [--project <name>] [--server <url>]` | Interactive Dragon session; lazily spawns daemon |
| `koboldlair analyze "<request>"` | Invokes Wyvern, prints task breakdown, persists into `.koboldlair/` in cwd |
| `koboldlair merge <runId>` | `git merge` the worktree from an earlier `do` run back into cwd's branch |
| `koboldlair status` | Daemon state + active agents + active runs |
| `koboldlair stop` | Stop the local daemon |
| `koboldlair login` | GitHub OAuth flow for remote daemons (writes `~/.koboldlair/auth.json`) |
| `koboldlair keys [create\|list\|revoke]` | Manage service-account JWTs on a remote daemon |
| `koboldlair projects [list\|create\|delete]` | Project CRUD shortcut |
| `koboldlair --version`, `koboldlair --help` | Standard meta verbs |

### Daemon interaction

- `do` / `run`: ephemeral — start `KoboldLair.Server` as a child process, run the verb, exit (server dies with parent)
- `chat` / `analyze`: discover daemon via `~/.koboldlair/daemon.json`; spawn one if missing
- `--server <url>` forces remote daemon, skips local discovery, requires OAuth token

### UX

- ASCII logo on startup (only for `chat`, not for `do`/`run` to keep scripting clean)
- Top header in interactive mode: active Kobolds count, current project, current daemon, tokens spent
- Color-coded status indicators + progress bars during runs
- Streaming output for `do` / `run` / `chat` (token-by-token, tool-call annotations)

## Distribution

- Initial release: `dotnet publish` artifacts attached to a GitHub release
- Follow-up tasks (out of scope of this story): chocolatey package, homebrew tap, scoop manifest

## Risks

- `do`'s "spawn ephemeral server then exit" has cold-start cost (~1.5s .NET startup). Document it; if user complains, switch `do` to "use daemon if available, ephemeral if not."
- The CLI binary statically includes the whole `KoboldLair.Server` to enable self-spawn — binary size will be ~80–100 MB after AOT trimming. Acceptable trade-off.
