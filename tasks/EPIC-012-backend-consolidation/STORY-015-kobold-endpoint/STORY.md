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

- Message shapes: `kobold_run_started` (`{ runId, worktree, mode }`), `kobold_stream`, `kobold_tool_call`, `kobold_reflect`, `kobold_complete`, `error`
- These mirror the Dragon stream shapes by convention, but they are **new** — Dragon's are produced by `DragonService`; Kobold has no equivalent producer yet (see below).

## Existing building blocks (use these)

| Concern | Use |
|---|---|
| WS endpoint + auth | `app.MapWebSocket("/kobold", ...)` — Birko.Communication.WebSocket extension (same one `/dragon`, `/wyvern` use). The `MapWebSocketEndpoint(..., requireAuthentication: true)` variant wires `WebSocketAuthenticationMiddleware`; reconcile with STORY-017's JWT (token via `?token=`). |
| Kobold execution | `KoboldFactory` (parallel-limit-aware creation) → `Kobold.EnsurePlanAsync(...)` then `Kobold.StartWorkingWithPlanAsync(...)` / `StartWorkingWithPlanEnhancedAsync(...)`. |
| Worktrees | `Drake.SetupFeatureBranchWorktreeAsync` / `CleanupWorktreeAsync` + `GitService` (per-repo `SemaphoreSlim` locks already serialize git ops). Project mode reuses these directly. |
| Old behaviour being replaced | `DraCode.WebSocket`'s `AgentConnectionManager` (retired in STORY-014) — `/kobold` ad-hoc mode is its successor. |

## ⚠️ Net-new: the run-event source (shared with STORY-016)

This is the core enabling work, not a detail. **Kobold today runs headless**: `StartWorkingWithPlanAsync` / `StartWorkingWithPlanEnhancedAsync` execute the full tool loop and return a `List<Message>` only at the end. The *only* progress callback Kobold exposes is `OnEscalation` (`Action<EscalationAlert>`); there is **no per-tool-call, per-reflection, or per-plan-step streaming hook**. `Agent.Options.EnableStreaming` covers raw LLM token streaming, and `OnLlmResponseReceived` is a no-arg activity ping — neither emits structured run events.

So the `kobold_stream` / `kobold_tool_call` / `kobold_reflect` protocol requires **adding an event sink into Kobold's tool loop** (the `reflect` and `update_plan_step` tools already produce the structured data internally — they need to publish it). This same event source is what **STORY-016's `/api/v1/runs/{id}/events` SSE consumes** — build it once as an internal per-run event stream (cf. `Birko.EventBus`) that both transports subscribe to: **one source of events, WS + SSE.**

## Open questions (decide during implementation)

- Should ad-hoc runs create a hidden project record in `projects.json`, or stay completely off-registry? Lean: off-registry, keyed only by `runId`, with cleanup after N days.
- How does the user pick agent type in ad-hoc mode? Auto-detect from cwd files (language stats) or require explicit `--agent csharp`?
- Two auth paths exist (Birko `WebSocketAuthenticationMiddleware` vs STORY-017's ASP.NET Core JWT bearer). Pick one for `/kobold` and apply it consistently across `/dragon`, `/wyvern` too.

## Suggested task breakdown

1. **Run-event source** — internal per-run event stream; wire Kobold's tool loop (`reflect`, `update_plan_step`, tool-call dispatch) to publish tool-call/reflection/step/completion events. *(Shared with STORY-016; build first.)*
2. **`/kobold` endpoint + protocol** — `MapWebSocket("/kobold")`, parse mode payload, subscribe the WS to the run-event source, emit `kobold_*` messages.
3. **Ad-hoc mode** — cwd validation, auto `git init`, worktree under `<cwd>/.koboldlair/.worktrees/r-<runId>/`, spawn via `KoboldFactory`, off-registry run tracking.
4. **Project mode** — load plan + reuse Drake worktree machinery, commit to feature branch on completion.
5. **PathHelper widening** for ad-hoc cwds outside the project sandbox (see risk).

## Risks

- Worktree creation in arbitrary cwds means the server touches paths outside KoboldLair's normal project sandbox. `PathHelper` validation needs to be widened (or explicitly bypassed) for `/kobold` ad-hoc mode — scope this carefully so it doesn't weaken sandboxing for the project pipeline.
- `git init` on a non-repo cwd is convenient but writes `.git/` into the user's directory. Document it loudly in the CLI verb's help.
- The run-event source touches the hot path of every Kobold execution; make publication non-blocking (drop/buffer if no subscriber) so streaming never stalls execution.
