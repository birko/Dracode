---
id: TASK-152
parent: STORY-046
feature: FEATURE-026
status: done
priority: P2
assignee: agent
created: 2026-11-16
depends-on: [TASK-151]
blocks: [TASK-160]
findings: []
pr: null
github-issue: null
jira-key: null
---

# Dragon widens with focus

## Context

Rebuilt, not evolved (FEATURE-026 D9): built on Birko.Web from the start, with no local copies of framework pieces — Birko components (`b-tabs`, `b-chat`, `b-stat`, Shell CRUD pages, …), Birko.Web.Core's `escapeHtml` and `BaseComponent.listen()`, the framework tokens and themes. Data comes only from `/api/v1` and `/dragon`, which are owner-scoped; `/wyvern` carries no caller identity, so nothing it returns may back a screen (D8). Layout mockups: https://claude.ai/artifact/Mb6tZiqZBbWkda3qYUikhs (C1 homepage, C2/C3 workspace, M1–M3 phone).

A thin chat is hard to read, so Dragon gets width only while in use (D13): it expands when focus enters it (a click,
or a keyboard shortcut) and shrinks when focus leaves, with no timers. Shrunk it is ~360px; expanded ~60% of the
width, pushing the centre aside. A "Keep expanded" pin holds it open and is remembered for the user across reloads
(where it is stored is the plan's choice). Below the ~900px breakpoint this does not apply: phones give Dragon its own
tab.

Decisions: FEATURE-026 D13.

## Acceptance criteria

- [x] Focus entering the dock (click or the shortcut, shown in its tooltip) expands it to ~60%; focus leaving shrinks it to ~360px; nothing changes on a timer
- [x] The transition takes ~150 ms and is instant under `prefers-reduced-motion`
- [x] The pin keeps it expanded and is remembered for the user after a reload
- [x] An e2e spec in `DraCode.KoboldLair.Client/tests/ui-e2e` covers it, red before the change; the suite stays green

## Out of scope

- The board's compact list while Dragon is expanded (TASK-153)

## Human test plan

- [x] Click into Dragon, type, click the board: it widens and narrows without jumping, and the typed text is kept
- [x] The width change feels quick but not abrupt; with reduced motion set in the OS it is instant

## Implementation plan

_Not drafted; the work followed the acceptance criteria directly._

## Notes

- **Layout** (`project-workspace.ts`): the workspace grid's Dragon column is 360px, or 60% of the workspace while
  `expanded` — focus inside the dock or the pin. `focusin` on the dock expands; `focusout` shrinks unless focus moved
  to another element in the dock, or the window itself lost focus (it comes back to the same place). The dock has
  `tabindex="-1"`, so a click on a part of it that takes no focus (its header, the message list) still focuses it. The
  grid columns transition over 150 ms, none under `prefers-reduced-motion`. Below 56rem the grid is one column either
  way. The class is toggled directly, so the workspace never re-renders for it.
- **Shortcut:** Alt+Shift+D (matched on `code`, since Alt+Shift+D types a character on a Mac) anywhere in the
  workspace puts focus in Dragon's input, or the dock itself while the input is disabled. It is in the dock header's
  tooltip and the dock's `aria-keyshortcuts`. Ctrl/Alt+D and Ctrl+Shift+D were avoided: the browser takes them.
- **Pin** ("📌 Keep expanded", `aria-pressed`) in the dock header, hidden below the breakpoint. Stored in localStorage
  per signed-in user (`koboldlair.dragon-pinned:{userId}`, `services/dock-layout.ts`): a layout preference of this
  browser, not worth a server setting; storage that cannot be read reads as unpinned. The dock tells the workspace
  through a `dragon-pin` event.
- **Board:** `b-kanban` stacks on its own width (TASK-183), so with Dragon wide the board turns compact by itself.
- **Tests:** e2e `dragon-focus.spec.ts`, 6 tests (5 red on the old code; the phone-width one passes either way). Suite
  79/79. No server change.
- **Human test** (throwaway Testing server on a scratch copy, JWT on, LAN model; `waver2`'s Board, 1400px): the dock's
  width sampled every frame went 360 → 815px over ~133 ms in 9 steps, always one way, and back the same; the typed text
  stayed; the board stacked while Dragon was wide and went back to columns. With reduced motion each change was one
  frame. Alt+Shift+D focused the input; pinned, Dragon stayed wide after a reload (dark), no console errors.
