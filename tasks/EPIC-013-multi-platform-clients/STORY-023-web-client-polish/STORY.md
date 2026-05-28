---
id: STORY-023
parent: EPIC-013
status: planned
created: 2026-05-28
---

# DraCode.KoboldLair.Client — UI polish reclaimed from EPIC-003

## User story

As a web UI user, I want the polish items that were planned for the (now-deleted) `DraCode.Web` retargeted to `DraCode.KoboldLair.Client`, plus a few new affordances the multi-platform rework exposes (daemon status, project switcher, OAuth login button).

## Behaviour

### Reclaimed from old EPIC-003

- **Drag-and-drop tab reordering** in the project workspace view
- **Workspace configuration save/load** — named layouts: which panels are open, splitter sizes, active tabs. Persist to user profile on the server.

### New from the multi-platform rework

- **Daemon status indicator** in the header — green dot when connected, red when daemon is down, click to view daemon info
- **Project switcher** in the header — fast switch across projects without losing chat scroll position (multi-session aware)
- **OAuth login button** replacing the existing token-paste field; integrates with STORY-017's GitHub OAuth flow
- **Service-account key management** UI under user settings → API Keys (matches CLI `koboldlair keys` verbs)
- **Run viewer** for ad-hoc `/kobold` runs that were started outside the web UI (e.g. by the CLI) — see them in a "Recent runs" panel, watch the SSE stream

## Risks

- Workspace config storage: needs a `user_settings` extension or new `user_workspaces` table — coordinate with STORY-017's `users` table
- Most existing features (tab system, panel layout) live in the vanilla-JS legacy UI but are being migrated to Shadow DOM components — confirm the target component framework before building drag-drop on a moving foundation
