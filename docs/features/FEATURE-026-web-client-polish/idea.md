---
id: FEATURE-026
created: 2026-05-31
owner: human
status: idea
---

# DraCode.KoboldLair.Client — UI polish reclaimed from EPIC-003

> Stakeholder-readable. A PM/stocktaker should understand the problem and shape without code.

## Problem

The web UI has a backlog of polish items that were planned for the old web client (now retired) and never delivered, plus the multi-platform rework has exposed new things users now expect to see — like whether the background service is connected, and a faster way to switch projects. Users want a more finished, multi-platform-aware web experience.

## Proposed shape

Bring the long-deferred polish to the current web client and add affordances the rework makes possible. Reclaimed items: drag-and-drop reordering of workspace tabs, and saveable/named workspace layouts (which panels are open, splitter sizes, active tabs) stored to the user's profile. New items: a service status indicator in the header (green when connected, red when down, click for details), a fast project switcher that doesn't lose chat scroll position, an OAuth sign-in button replacing the old token-paste field, a key-management screen under settings, and a "recent runs" panel to watch runs that were started outside the web UI (for example from the CLI).

## Out of scope (initial)

- Building drag-and-drop before the target UI component foundation is confirmed (some pieces are mid-migration to the new component framework)
- Storage schema choices that aren't coordinated with the upcoming users/profile work

## Prototype
- Pending — backlog item; prototype decision deferred to /feature decide.
