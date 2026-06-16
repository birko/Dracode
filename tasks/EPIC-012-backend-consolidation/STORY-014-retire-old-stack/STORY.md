---
id: STORY-014
parent: EPIC-012
status: done
created: 2026-05-28
---

# Retire DraCode.WebSocket and DraCode.Web

## User story

As a maintainer, I want the old single-agent stack removed so that there is one obvious backend (`KoboldLair.Server`) and one obvious web client (`KoboldLair.Client`), with no dead code that still compiles and confuses contributors.

## Blast radius (verified)

A repo-wide grep for `DraCode.WebSocket` / `DraCode.Web` confirms the cut is clean — **no `DraCode.KoboldLair*` project references the old stack**, and the CLI (`DraCode`) doesn't either. References fall into three buckets:

**1. Code/project files to delete or edit:**
- Delete folders `DraCode.WebSocket/` and `DraCode.Web/` (all source is self-contained there — `AgentConnectionManager`, `WebSocketMessageValidator`, `AgentConnectionEvent`, models, `wwwroot`, `.http`).
- `DraCode.slnx` — remove both project entries.
- **`DraCode.AppHost/DraCode.AppHost.csproj`** — remove the two `<ProjectReference>` lines (⚠️ the original draft missed this; without it AppHost won't compile).
- **`DraCode.AppHost/AppHost.cs`** (⚠️ not `Program.cs` — Aspire 13 single-file style) — remove the `dracode-websocket` + `dracode-web` `AddProject` blocks (and the `web → websocket` `.WithReference`) plus the "WebSocket System Group" comment header.

**2. `DraCode.ServiceDefaults`:** grep shows the old stack references ServiceDefaults, but **ServiceDefaults does not reference the old stack** — it's shared with KoboldLair. Likely nothing to remove; verify no old-stack-only helpers exist before touching it.

**3. Docs to scrub or delete:**
- Delete (old-stack-specific): `docs/setup-guides/WEBSOCKET_QUICKSTART.md`, `docs/setup-guides/WEB_CLIENT_MULTI_PROVIDER_GUIDE.md`, `DraCode.WebSocket/README.md`, `DraCode.Web/README.md`, `DraCode.AppHost/README.md` (rewrite this one).
- Scrub references: `README.md`, `docs/README.md`, `docs/FULL_PROJECT_SPECIFICATION.md`, `docs/DEVELOPMENT_WORKFLOW.md`, `docs/troubleshooting/TROUBLESHOOTING.md`, `docs/CHANGELOG.md`.
- **`CLAUDE.md`**: drop the two rows from the "Project Structure" table and change the **"9 projects" → "7 projects"** count; remove the `dotnet run --project DraCode.WebSocket` and `DraCode.Web` build commands; drop the `/ws` endpoint + "port 5000 single-agent server" mentions.

## Feature alignment

This story is backed by **FEATURE-016-retire-old-stack** (`docs/features/FEATURE-016-retire-old-stack/`) — keep its `status.md` in sync when this lands. (Historical context lives in `FEATURE-072-websocket-multi-agent-system`, the original old-stack feature.)

## Ordering

Can ship **first** within the epic — the `/kobold` endpoint (STORY-015) replaces the WS use case, but until then the old stack is merely unused, not blocking. Mechanical deletion PR; no dependency on 015/016/017.

## Risks

- Hidden depends-on in tests or sample scripts — the grep above is the guard; re-run it immediately before deleting to catch anything added since.
- Anyone using the bare `/ws` endpoint loses it. `/kobold` (STORY-015) is the migration target — its protocol is a near-superset of the old `connect/send` flow.
- The `.csproj` `ProjectReference` removal and the `AppHost.cs` `AddProject` removal **must land in the same commit** — Aspire generates `Projects.DraCode_WebSocket` / `Projects.DraCode_Web` types from the references, so editing one without the other breaks the build.
