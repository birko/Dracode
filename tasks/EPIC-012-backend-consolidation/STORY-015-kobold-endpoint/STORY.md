---
id: STORY-015
parent: EPIC-012
status: planned
created: 2026-05-28
---

# /kobold WebSocket endpoint — ad-hoc + project-scoped Kobold execution

## User story

As a CLI or scripting caller, I want a server endpoint that runs a single Kobold for me — either ad-hoc against my current directory or against an already-analyzed project task — so that I get the single-agent behaviour the old `DraCode.WebSocket` offered, plus the option to drive the project pipeline from non-browser clients.

## Behaviour

New endpoint `app.MapWebSocket("/kobold", ...)` on `DraCode.KoboldLair.Server`. Two operating modes via the initial message payload.

### Ad-hoc mode

- Payload: `{ mode: "adhoc", cwd: "<absolute-path>", prompt: "<request>", agentType?: "<kobold-type>" }`
- Server validates cwd exists
- If `cwd` is not a git repository, automatically `git init` + initial commit of existing files
- Always create a git worktree under `<cwd>/.koboldlair/.worktrees/r-<runId>/`
- Spawn a Kobold in the worktree, stream tool calls and reflections back over the WS
- On completion respond with `{ status: "completed", worktree: "...", runId: "r-..." }`
- User is responsible for `koboldlair merge r-...` (or manual `git merge`) to apply changes to cwd

### Project mode

- Payload: `{ mode: "project", projectId: "...", taskId: "..." }`
- Server loads the existing plan + worktree from project state (reuses Drake's worktree machinery)
- Streams Kobold execution identically to ad-hoc mode
- On completion, commits to the project's feature branch (existing Drake behaviour)

### Message protocol

- Reuse the existing Dragon stream message shapes where they apply: `kobold_stream`, `kobold_tool_call`, `kobold_reflect`, `kobold_complete`, `error`
- Add `kobold_run_started` with `{ runId, worktree, mode }` so clients can show progress immediately

## Open questions (decide during implementation)

- Should ad-hoc runs create a hidden project record in `projects.json`, or stay completely off-registry? Lean: off-registry, keyed only by `runId`, with cleanup after N days.
- How does the user pick agent type in ad-hoc mode? Auto-detect from cwd files (language stats) or require explicit `--agent csharp`?

## Risks

- Worktree creation in arbitrary cwds means the server touches paths outside KoboldLair's normal project sandbox. `PathHelper` validation needs to be widened or bypassed for `/kobold` ad-hoc mode.
- `git init` on a non-repo cwd is convenient but writes `.git/` into the user's directory. Document it loudly in the CLI verb's help.
