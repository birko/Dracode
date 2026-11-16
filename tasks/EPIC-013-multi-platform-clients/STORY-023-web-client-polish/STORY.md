---
id: STORY-023
parent: EPIC-013
status: in-progress
created: 2026-05-28
---

# Web UI: app frame, sign-in and project picker

> Retitled 2026-11-16 by `/feature decompose FEATURE-026`. It was "UI polish reclaimed from EPIC-003". The layout
> grill (FEATURE-026 D8–D24) chose to rebuild the web UI instead of polishing it. This story now holds the frame and
> sign-in, and the rebuild's other parts are STORY-045 to STORY-050. The original behaviour list is kept below for
> the record. Where each item went: tabs and saved layouts are TASK-058 (parked, D1/D2 proposed); daemon status is
> D3 (proposed); the project switcher is TASK-146; OAuth login is TASK-056; API keys are TASK-162; the run viewer is
> TASK-155 and TASK-057.

## User story (rebuild)

As a KoboldLair user, I want to sign in with GitHub and land on my projects in one clear frame, so that the web UI
is mine and I can get to any project from anywhere in it.

## Behaviour (rebuild)

- A header over the page, and the projects homepage as home (D9, D10, D22)
- Sign in with GitHub; each user sees their own projects (D5, D8)
- A project picker in the header inside a project (D10)

## Original story (2026-05-28)


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
