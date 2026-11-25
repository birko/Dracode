---
id: TASK-198
parent: STORY-023
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-197]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-043]
pr: null
github-issue: null
jira-key: null
---

# The workspace divider stops at the tabs' and Dragon's minimums while it is dragged

## Context

Split from TASK-197 on 2026-11-25. That task fixed the divider (no gap, the dragged width kept) but not its minimums.

- `b-split-panel` holds a live drag only to its own 120 px per column. The workspace's minimums (tabs 320 px, Dragon
  280 px, `MIN_TABS_PX` / `MIN_DRAGON_PX` in `project-workspace.ts`) are applied by `resized()` after the drop.
- So a drag past them ends with an animated correction, measured in TASK-197's human test: 1264→1064 px, and 120→280
  px.
- Birko TASK-981 (filed by the `birko-framework-44` session, 2026-11-25) adds `master-min` and `detail-min`. The drag
  and the arrow keys stop at them.

## Acceptance criteria

- [x] The workspace sets the panel's minimums from `MIN_TABS_PX` and `MIN_DRAGON_PX`. A mouse drag past either stops
      there while it is dragged, and nothing moves after the drop. `layouts.spec.ts`'s minimum test checks every frame
      after the drop.
- [x] `resized()` keeps its clamp for a stored width that no longer fits (a narrower window).

## Out of scope

- The minimums themselves: Birko TASK-981

## Human test plan

- [x] Drag the divider far both ways with a mouse: it stops at 320 px for the tabs and 280 px for Dragon, with no
      movement on release.

## Implementation plan

1. Red first, in `layouts.spec.ts`: one test per minimum (the tabs' 320 px, Dragon's 280 px). Each drags past the
   minimum with a mouse, checks the column never goes below it while dragging, and checks every frame for 400 ms
   after the drop. Red without the attributes: the tabs reached 184 px and Dragon 120.
2. `project-workspace.ts`: `master-min="${MIN_TABS_PX}px" detail-min="${MIN_DRAGON_PX}px"` on the panel.
3. Birko TASK-981 merged with widths in rem (Birko.Web main 4dc4e0c), which made today's `resized()` drop every
   dragged width again (nothing stored). Red against it: 4 tests.
   - `resized()` now converts rem or px through `toPx`.
   - It keeps the panel's value when it is within the minimums, and stores a corrected width in px.
   - The tests compare stored widths in px (`storedDragon`), and the keyboard step is 4rem at the root font size.
4. CLAUDE.md "Workspace layouts" describes the divider.

## Notes

- **Birko TASK-981** (`birko-framework-44`) is on Birko.Web main 4dc4e0c (tag v0.5.3, not pushed). Its test step was run
  from here against its worktree first (a0083e9), and then on main.
- **The root font size here is 14 px**, so 1rem is 14 px. The rem conversion reads it rather than assuming 16.
- **Human test on a throwaway server** (the TASK-163 copy of `greeter175`), with a mouse and the keys:
  - Drags past either minimum stopped there while dragging: the tabs at 320 px (Dragon 1064), and Dragon at 280 px.
  - There was no gap at any step, and nothing moved after release.
  - Home and End gave tabs 320 / Dragon 1064 and tabs 1104 / Dragon 280. The arrow keys stopped at those limits.
  - A reload kept 480 px, and a wider window left it alone.
  - The stored layout read `"detail":"20rem"`. No page errors.
- Full e2e suite 187 passed (nothing on 57087). No .NET code changed.
