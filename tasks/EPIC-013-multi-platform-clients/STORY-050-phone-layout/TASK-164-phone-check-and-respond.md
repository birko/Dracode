---
id: TASK-164
parent: STORY-050
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-145, TASK-151, TASK-153, TASK-159]
blocks: []
findings: []
pr: 58
github-issue: null
jira-key: null
---

# Phone check-and-respond layout

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

Below the ~900px breakpoint the app is for checking and responding (D21): projects and progress, a read-only board,
Dragon chat and decisions, notifications. A bottom tab bar switches between them, with Dragon as its own tab (D22).
Git, settings, API keys and reprioritising stay desktop: those controls are not shown on phones. On tablet widths the
desktop layout stays, with Dragon opening as an overlay instead of docking (the plan sets the width range).

Decisions: FEATURE-026 D21, FEATURE-026 D22.

## Acceptance criteria

- [x] Below ~900px a bottom tab bar offers Projects, Board, Dragon and Notifications
- [x] The board is read only on phones: no drag, no Retry; Git, Settings and API keys are not reachable
- [x] Dragon's tab shows the chat and decision cards full screen
- [x] On tablet widths Dragon opens as an overlay over the desktop layout
- [x] An e2e spec at phone and tablet viewports covers it; the suite stays green

## Out of scope

- Phone push (TASK-144, deferred)

## Notes

- **The mockups (M1–M3) predate D21/D22.** They show Projects/Dragon/Costs and Board/Dragon/Runs/More. I built the
  task's tab set and drew the open question as three new frames on the canvas for the user to pick from.
- **The overlay is CSS over the one dock, not `b-drawer`.** Moving the dock into a drawer per breakpoint would
  rebuild it and reload its conversation; the dock already shares the page's Dragon connection, so showing it fixed
  keeps everything.
- **Seven older tests asserted the layout this replaces at 375px:** Dragon under the tabs, the home Dragon under the
  cards, the picker, the pill and the bell in a phone's header, and `#/dragon` as not-found (the old Dragon screen).
  - Each is rewritten for the new layout rather than deleted.
  - The picker, pill and bell checks moved to 900px, the narrowest window that shows them.
  - The pill's short "⚑ 2" form could no longer show anywhere and is removed.
- **Human test on a throwaway server** (the TASK-163 copy of `greeter175`), emulated devices:
  - **Phone:**
    - The tabs read Projects, Board (greyed out), Dragon, Notifications with the unread count.
    - Opening the project gave Board, Spec, Runs and Stats, with no draggable card and no Retry.
    - Dragon's tab answered "How far along is this project?" about greeter175.
    - Opening a notification marked it read and opened the board.
    - `#/settings` said it is on a computer.
    - No horizontal scroll and no page errors.
  - **Tablet (1024px):** no tab bar, Git offered, Dragon hidden until "🐉 Dragon", then over the board, which stayed
    visible; ✕ closed it.
  - **Throwaway setup:** the TASK-163 test's $0.25 daily budget first refused Dragon there, as it should, and was set
    back to none through `PUT /api/v1/budgets` before the run.
- **Real phone** (Mi A2 Lite, Android 10, Chrome, a 320px-wide viewport), driven over Chrome's DevTools socket with
  `adb reverse` for 57087 and 57088:
  - Projects, the read-only board, Stats, Dragon and Notifications all worked, with no horizontal scroll and no page
    errors. Auto followed the phone's dark mode.
  - Dragon answered "Is anything waiting on me in this project?" about greeter175.
  - It found one defect, fixed after the merge: the Dragon page was 2px taller than the space between the header and
    the tab bar, so the page scrolled. Its height now also takes off the two 1px borders and the bottom safe area.
- The e2e suite is not sealed from a live server on 57087: with the throwaway server running, two home-dragon tests
  failed, and with it stopped all 176 pass. Run the suite with no server on 57087.
- Full e2e suite 176 passed (one fewer: `#/dragon` is a route again); full .NET suite 632 passed (no server change).
  `tsc --noEmit` reports the same 32 errors as before, all in Birko.Web's sources.

## Human test plan

- [x] On a phone (emulated iPhone 13, then a real Android phone, 2026-11-24) against a local server: check a project's progress, answer Dragon, open a notification
- [x] On a tablet (emulated iPad Mini landscape): Dragon opens over the board and closes again

## Implementation plan

Decided with the user (2026-11-24), after prototypes on the layout canvas (row "TASK-164 phone workspace", frames A,
B1 and B2):

- Phone below 900px, tablet 900–1279px, desktop from 1280px (`services/layout-mode.ts`: `PHONE_MEDIA`, `TABLET_MEDIA`,
  `layoutMode()`, `onLayoutModeChange()`).
- Board and Dragon follow the last project opened, kept for the session (`rememberProject` / `lastProject`). With none,
  Board is greyed out and Dragon is the home Dragon.
- Option B: the phone workspace keeps Board, Spec, Runs and Stats, all read only; only Git is left out.

1. Red first: `tests/ui-e2e/phone.spec.ts` (9 tests at 390×844, 1024×768 and 1440×900).
2. Shell (`app-shell.ts`): `renderFooter()` draws the tab bar on Birko's `Surface` / `activeSurface` model, redrawn alone
   on a hash change, a new last project or new notifications. Below 900px the picker, the pill and the bell are hidden,
   and `getUserMenuItems()` drops Settings.
3. Routes `#/dragon` (`dragon-page.ts`: the dock full screen, "About <project>") and `#/notifications`
   (`notifications-page.ts`: the bell's list full screen, unread first; opening one marks it read and opens the board).
4. Workspace: no dock on a phone, Git left out of the tabs and a link to it answered "Git is on a computer". It remembers
   the project it opens.
5. Board: on a phone the whole `b-kanban` is locked (its `locked` config), failed cards have no Retry, and a note says
   so. It re-configures when the window crosses the breakpoint.
6. Tablet overlay (`services/dock-overlay.ts`), shared by the homepage and the workspace:
   - The dock stays in the page and is shown `position: fixed` over the right side.
   - It is opened by a "🐉 Dragon" button or Alt+Shift+D, and closed by its ✕, Escape or the button.
   - Focus goes into Dragon on opening and back to the button on closing.
   - The "Keep expanded" pin shows only where Dragon docks (1280px and wider).
7. Settings on a phone says it is on a computer; seven earlier narrow-width tests rewritten for the new layout.
