---
id: TASK-029
parent: STORY-014
feature: FEATURE-016
status: done
priority: P1
assignee: ai
created: 2026-06-11
depends-on: []
blocks: [TASK-070]
pr: 14e6331
github-issue: null
jira-key: null
---

# Retire DraCode.WebSocket and DraCode.Web

## Context

Single mechanical PR removing the unmaintained old single-agent stack so there is one backend (`KoboldLair.Server`) and one web client (`KoboldLair.Client`). A repo-wide grep confirmed **no `DraCode.KoboldLair*` or CLI source references the old stack** — the cut is clean. See STORY-014 for the full verified blast radius.

## Acceptance criteria

- [x] `DraCode.WebSocket/` and `DraCode.Web/` folders deleted
- [x] Both entries removed from `DraCode.slnx`
- [x] `DraCode.AppHost/DraCode.AppHost.csproj` — two `<ProjectReference>`s removed **and** `DraCode.AppHost/AppHost.cs` `dracode-websocket`/`dracode-web` `AddProject` blocks removed in the **same commit** (Aspire generates `Projects.DraCode_WebSocket`/`_Web` types from the references)
- [x] Old-stack-only docs deleted (`WEBSOCKET_QUICKSTART.md`, `WEB_CLIENT_MULTI_PROVIDER_GUIDE.md`, and the two folder READMEs `DraCode.WebSocket/README.md` + `DraCode.Web/README.md`); `DraCode.AppHost/README.md` is **rewritten** (AppHost survives), not deleted; references scrubbed from `README.md`, `docs/README.md`, `docs/DEVELOPMENT_WORKFLOW.md` (scrub the DraCode.Web halves, keep KoboldLair.Client), `docs/troubleshooting/TROUBLESHOOTING.md`, `CLAUDE.md`. **`docs/FULL_PROJECT_SPECIFICATION.md` is out of scope here — split to TASK-070** (large regeneration-spec rewrite, no build coupling)
- [x] `CLAUDE.md` "Project Structure" table **corrected to reflect the real solution** (true count **8 projects**): drop the two old-stack rows AND the fictional `DraCode.Agent` row, add the missing `DraCode.Birko` + `DraCode.KoboldLair.Tests` rows; old `dotnet run` build commands and `/ws`/port-5000 mentions removed
- [x] `FEATURE-016` `status.md` updated
- [x] `.vscode/tasks.json` — `build-websocket`, `build-web`, `watch-websocket`, `watch-web` tasks removed
- [x] `.vscode/launch.json` — "Launch WebSocket Service" + "Launch Web Client" configs removed; the `"Launch All Services"` compound **repointed** to `["Launch KobolLair Server", "Launch KobolLair Client"]` (not left dangling)
- [x] `dotnet build ./DraCode.slnx` succeeds after removal (verified: 0 errors; 195 pre-existing CS0436 Birko warnings, unrelated)

## Out of scope

- Building `/kobold` as the migration target (STORY-015 / TASK-038)
- Touching `DraCode.ServiceDefaults` unless an old-stack-only helper is found (it's shared; likely nothing)

## Human test plan

- [x] `dotnet run --project DraCode.AppHost` → Aspire dashboard shows only the two KoboldLair resources, no websocket/web group, and starts cleanly (verified 2026-06-12: dashboard listed only `dracode-koboldlair-server` + `dracode-koboldlair-client`)

## Implementation plan

> ✅ **Resolved (README handling):** `DraCode.AppHost/README.md` is **rewritten** (AppHost survives the cut — strip the two old-stack resources, keep KoboldLair). The two old-stack folder READMEs (`DraCode.WebSocket/README.md`, `DraCode.Web/README.md`) are **deleted** with their folders. AC bullet updated to match.

### Verified current state
- `DraCode.slnx` lists both old-stack projects at lines **34–35**.
- `DraCode.AppHost/DraCode.AppHost.csproj` references both at lines **4–5**.
- `DraCode.AppHost/AppHost.cs` (Aspire 13 single-file — **not** `Program.cs`) holds the WebSocket group: comment banner + `websocket`/`web` `AddProject` blocks at lines **29–39** (incl. `web → websocket .WithReference`).
- **ServiceDefaults is clean**: the old stack *consumes* it, but it doesn't reference the old stack → no edits (matches out-of-scope).
- All non-doc code references live **inside the two doomed folders**. No test/CI/script references outside. Clean cut confirmed.

### Ordering & atomicity (single commit)
Aspire source-generates `Projects.DraCode_WebSocket` / `Projects.DraCode_Web` from the `<ProjectReference>` lines, consumed by `AppHost.cs`. The `.csproj` removal, the `AppHost.cs` `AddProject` removal, and the folder/slnx deletion **must land in one commit**. Sequence:
1. `AppHost.cs` — remove lines 29–39 + the "WebSocket System Group" banner; keep KoboldLair blocks + `builder.Build().Run();`.
2. `DraCode.AppHost.csproj` — delete the two `<ProjectReference>`s (lines 4–5).
3. `DraCode.slnx` — delete lines 34–35.
4. `git rm -r DraCode.WebSocket/ DraCode.Web/` (removes those two READMEs too).
5. Docs scrub/delete (below) — same commit.

### .vscode cleanup (dead dev wiring — doesn't break the build, but is exactly the "dead config" this task kills)
- `.vscode/tasks.json`: delete the 4 old-stack tasks — `build-websocket` (lines 41–53), `build-web` (54–66), `watch-websocket` (100–112), `watch-web` (113–125).
- `.vscode/launch.json`: delete "Launch WebSocket Service" (port 5000, lines 64–83) + "Launch Web Client" (port 5001, lines 84–103); **repoint** the `"Launch All Services"` compound (lines 124–138) to `["Launch KobolLair Server", "Launch KobolLair Client"]` (fix the existing `KobolLair` typo in those config names while there, if cheap).
- Note: `dotnet build ./DraCode.slnx` does **not** read `.vscode/`, so the build-verification AC won't catch a miss here — verify these by hand (open the Run/Debug menu, confirm no dangling configs).

### Doc deletions
- `docs/setup-guides/WEBSOCKET_QUICKSTART.md` — delete.
- `docs/setup-guides/WEB_CLIENT_MULTI_PROVIDER_GUIDE.md` — delete.

### Doc scrubs (edit, keep file)
- **`CLAUDE.md`**: lines 15–16 (old `dotnet run` cmds), 23–24 (TS build block), 37–38 (old-stack table rows), 224 (TS-5.7-for-DraCode.Web line), 671–672 (port-5000 curl). **Correct the Project Structure table to reality** (the table is already stale): the count is **8** (not the doc's "9"→"7"); also remove the fictional `DraCode.Agent` row (no such project — agents live in `DraCode.Birko`) and add the missing `DraCode.Birko` + `DraCode.KoboldLair.Tests` rows. **Keep** the KoboldLair `### WebSocket Endpoints` `/wyvern`+`/dragon` section — that's KoboldLair's transport, not the old stack.
- **`README.md`**: lines 22, 161, 174, 179, 184–185, 187, 311, 389, 392, 427 — remove old-stack links, `dotnet run` lines, `ws://localhost:5000/ws` / `:5001` URLs, tree entries, README links.
- **`docs/README.md`**: lines 135–136 + 154–155 (links to deleted files/folders).
- **`docs/DEVELOPMENT_WORKFLOW.md`**: **scrub, do not delete** — verified it documents *both* DraCode.Web and KoboldLair.Client TS workflows. Remove the DraCode.Web halves (Quick Start Option 1/2, the `### DraCode.Web` subsection, "Full Development Setup" Terminal 2 + port 5001, "What's Watched → DraCode.Web", the `cd DraCode.Web && npx tsc` in Production Build, and the embedded `.vscode` example snippets that reference `dracode-web`/DraCode.Web at lines ~186–233). Keep all KoboldLair.Client content.
- **`docs/troubleshooting/TROUBLESHOOTING.md`**: lines 36, 41, 45, 100 (old-stack ws/`wscat` steps).
- **`docs/FULL_PROJECT_SPECIFICATION.md`** — **deferred to TASK-070** (large prose rewrite of a regeneration spec; no build/runtime coupling, so it doesn't belong in this mechanical deletion PR).
- **`DraCode.AppHost/README.md`**: **rewrite** (see ⚠) — strip WebSocket resource group, diagram nodes, snippet lines, port/publish/troubleshooting blocks; keep KoboldLair.

### Leave alone (deliberately)
`docs/CHANGELOG.md` (append-only history), `docs/features/FEATURE-072/069` (historical old-stack records), `docs/Dragon-Requirements-Agent.md:902` (generic, applies to KoboldLair), all `tasks/**` + FEATURE-016/017 tracking files.

### FEATURE-016 status
Update `docs/features/FEATURE-016-retire-old-stack/status.md`: phase idea → in-progress/done, progress line to `1/1 tasks done` on merge, refresh "what can be tested" / "next step".

### Risks
- **Aspire codegen coupling** — split commits break the build; keep it atomic.
- **Stale refs added since planning** — re-run grep `DraCode\.WebSocket|DraCode\.Web|dracode-websocket|dracode-web` (all file types) immediately before deleting.
- **Over-scrubbing** — KoboldLair legitimately uses WebSocket transport; only remove bare `/ws`, port 5000/5001, and `DraCode.WebSocket`/`DraCode.Web` named refs.
- `DEVELOPMENT_WORKFLOW.md` + `FULL_PROJECT_SPECIFICATION.md` are large rewrites — budget review time.

### Verification
From `C:\Source\DraCode`: `dotnet build ./DraCode.slnx` must succeed (proves Aspire codegen types are gone cleanly). Then `dotnet run --project DraCode.AppHost` → dashboard shows only the two `dracode-koboldlair-*` resources. Finally re-run the reference grep → zero hits outside CHANGELOG/historical-feature/task files.

### Critical files
`DraCode.AppHost/AppHost.cs` · `DraCode.AppHost/DraCode.AppHost.csproj` · `DraCode.slnx` · `CLAUDE.md` · `docs/features/FEATURE-016-retire-old-stack/status.md`
