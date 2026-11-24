---
id: TASK-058
parent: STORY-023
feature: FEATURE-026
status: done
priority: P2
assignee: ai
created: 2026-06-11
depends-on: [TASK-034]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# Drag-and-drop tabs + workspace layout save/load

> Parked 2026-11-16: this task is FEATURE-026 D1 and D2, both still `proposed`. The web UI is being rebuilt around the
> decided workspace (FEATURE-026 D9, D11), so if D1/D2 are approved, rewrite this task against the new workspace
> (TASK-150) before picking it.

## Context

> **Build on the framework (2026-10-07 review):** tab drag-and-drop, split sizes and the layout serialize/restore hook are Birko TASK-525 (`b-tabs` / `b-split-panel`); only the server-side per-user layout storage stays here.

The polish items reclaimed from the deleted EPIC-003. Drag-and-drop tab reordering in the project workspace view, and named workspace layouts (open panels, splitter sizes, active tabs) persisted to the user profile on the server. Builds on the existing Shadow-DOM client; persistence coordinates with STORY-017's `users` table (TASK-034) — likely a `user_workspaces` table or `user_settings` extension.

## Acceptance criteria

- [x] Drag-and-drop reordering of tabs in the workspace view; order persists across reloads
- [x] Named workspace layouts: save current layout (panels/splitters/active tabs), load by name, delete
- [x] Layouts persisted server-side keyed to the authenticated user (coordinate schema with TASK-034)
- [x] Tests where supported

## Out of scope

- The `users` table itself (TASK-034)
- Other polish items (TASK-055/056/057)

## Human test plan

- [x] Reorder tabs by dragging → reload → order preserved; save a layout "wide", change it, load "wide" → original layout restored; confirm it persists across a logout/login (server-side, per user)

## Implementation plan

Decided with the user (2026-11-24), after prototypes on the layout canvas (row "TASK-058 workspace layouts", frames A
and B):

- A mix of both frames: a divider sets Dragon's width at rest, and focus or the pin still widen it to ~60% (TASK-152,
  D13) and give that width back.
- Tab order and layouts are each person's own, across all their projects.
- A Layouts menu beside the project's name, not on a phone.

1. Red first:
   - `LayoutEndpointsTests` (7 tests): the current layout, named layouts per person, a restart, bad names and bodies,
     the cap, `view_own`, and a server without SQLite.
   - `tests/ui-e2e/layouts.spec.ts` (8 tests at desktop, tablet and phone widths).
2. Server: `user_layouts` (`UserLayoutEntity`, `SqlLayoutRepository`, covered by `SqliteJournalModeTests`) and
   `LayoutEndpoints`:
   - `GET`/`PUT /api/v1/layouts/current`.
   - `GET /api/v1/layouts`, `PUT`/`DELETE /api/v1/layouts/{name}`.
   - Keyed by the caller's subject. A layout is the client's JSON object, kept as sent (at most 16 KB).
   - Names are 1–60 characters, 50 per person; a save over an existing name is always allowed.
   - 403 without an identity; 409 on a server without SQLite.
3. Client:
   - **Store:** `stores/layout-store.ts` reads the current layout once per page and saves changes after 400 ms. A
     layout has Birko's `LayoutState` shape (`tabs` / `splits` by `layout-key`) plus `openTab` and `dragonPinned`.
     The Dragon pin moved into it (`dock-layout.ts`), with localStorage kept as the fallback.
   - **Split panel:** the workspace is a `b-split-panel` (`collapse-at` 1279.98px, so tablets keep the TASK-164
     overlay and phones their tab bar). A moved divider is kept as Dragon's own width (panel − tabs column − gap),
     at least 280px and leaving the tabs 320px.
   - **Widening:** focus or the pin set `{ 1fr, 60% }` and remove the divider while Dragon is wide.
   - **Tabs:** `b-tabs reorderable` (not on a phone); a `tab-reorder` saves the order, and a phone shows that order
     without Git.
   - **Layouts menu:** `b-dropdown-menu` with "Save current as…" (an inline name form), each saved layout to load,
     and "Delete “name”". Loading makes the layout current, sets the pin through `DragonDock.setPinned`, and opens its
     tab.
   - **Tests:** the e2e fixtures serve an in-memory current layout to every spec.

## Notes

- **The widening animation is waiting on Birko.Web.**
  - TASK-152's ~150 ms widening was a transition on DraCode's own grid. The columns are now b-split-panel's, inside
    its shadow root, which has no transition, so Dragon currently widens at once.
  - The user chose a Birko fix, asked of the `birko-framework-44` session (it had uncommitted Birko.Web work on
    `main`, so this session did not touch that tree): an opt-in `--b-split-transition` on the panel's columns, 0s by
    default and off while dragging.
  - The workspace already sets it (150 ms, 0s under reduced motion), and `dragon-focus.spec.ts` checks the variable
    until then.
- **Human test on a throwaway server** (the TASK-163 copy of `greeter175`, as `owner-175`), with a mouse:
  - Dragging the divider about 200 px left took Dragon from 360 to 563 px.
  - Dragging Git before Spec reordered the tabs.
  - Focusing Dragon widened it to 839 px, and it went back to 563 px.
  - Saved as "Reviewing" on the Git tab, then a reload kept all of it.
  - Then I moved Stats first and Dragon to 316 px. Loading "Reviewing" went back to Git with the saved order and
    563 px.
  - A fresh browser for the same person (no local storage) opened the same layout, which checks the task's
    log-out-and-in case.
  - Deleting worked, and there were no page errors.
- **Touch:** dragging a tab with a finger on a tablet was not tried. Birko's reorder uses HTML5 drag-and-drop, which
  touch browsers may not start. The keyboard path (Ctrl+Shift+←/→ on the open tab) works everywhere.
- **The keyboard moves the open tab, not the focused one** (Birko's `b-tabs`); the tests open the tab they move.
- Full .NET suite 640 passed; full e2e suite 184 passed (with nothing on port 57087). `tsc --noEmit` reports the same
  32 errors as before, all in Birko.Web's sources.
