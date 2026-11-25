---
id: TASK-197
parent: STORY-023
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-24
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-058]
blocks: [TASK-198]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-043]
pr: null
github-issue: null
jira-key: null
---

# Dragging the workspace divider leaves a gap beside Dragon, and the dragged width is lost

## Context

Found 2026-11-24 in the human test of Birko TASK-979, which added `--b-split-transition` for TASK-058. The test was
run in DraCode on a throwaway server, with every frame sampled. This is a defect in TASK-058's own divider. That task's
human test measured only after the drop, so it did not see this.

- **What happens.** While the divider is dragged, Dragon keeps its width and slides left as a block, leaving an empty
  strip on the right of the workspace. On the drop it stretches to fill the strip.
- **It is worse since TASK-979.** The stretch used to be instant. Now it animates (963→1064 px over about 130 ms), so
  the drop visibly moves.
- **Cause.**
  - The workspace sets `b-split-panel` sizes to `{ master: '1fr', detail: '<Dragon px>' }`, so Dragon keeps its width
    when the window changes (`project-workspace.ts`, `REST_SIZES` and `resized()`).
  - Birko's pointer drag (`Birko.Web.Components/src/layout/b-split-panel.ts`, the `pointermove` handler) sets only
    `--_master-w` in px and leaves `--_detail-w` at that fixed length.
  - On `split-resize`, the workspace turns the committed master width back into `{1fr, Npx}`, which is the stretch.
- **The drag is not held to the minimum.** It can push the tabs column below `MIN_TABS_PX` (320; 170 px was seen),
  and it springs back only on the drop.
- **Where it was reported.** The `birko-framework-44` session (Birko TASK-979) has the trace and a suggested Birko
  fix: while resizing, if the detail width is not `fr`, make it `1fr` (or drag the detail size instead).
- **Birko fixed its side (2026-11-24, Birko.Web main 0b3e085, v0.5.3).**
  - A panel shaped like the workspace's (flexible master, fixed detail) now resizes the detail column, so the strip
    is gone (0 px gap at every step, re-tested in DraCode).
  - The drop emits `{ master: '1fr', detail: 'Npx' }` itself.
- **Built against that Birko, DraCode now loses the dragged width.**
  - `resized()` in `project-workspace.ts` reads `parseFloat(split.sizes.master)`, gets NaN from `'1fr'`, and returns,
    so the stored width wins: a drag to 614 px animates back to 1064 px.
  - What is left here is to read the detail px (keeping the master-px path for an older Birko or none), clamp it, and
    store it.
- **The 320 px minimum.** Birko clamps a live drag only to 120 px per column, so a drag past DraCode's minimum is
  corrected, with an animation, at the drop. The Birko session offered a `min` attribute; DraCode asked for it to be
  filed.

## Acceptance criteria

- [x] While the divider is dragged, Dragon fills the space right of the divider at every step: no empty strip. A test
      samples Dragon's right edge during a drag and fails today.
- [x] The width a drag leaves is kept, and is restored after a reload. With Birko.Web 0b3e085 it springs back to the
      stored width today; a test pins it.
- [x] The drop does not visibly move the divider or Dragon: the width after the drop equals the width the drag left
      (it is not animated to it). The test checks this too.
- [ ] The tabs column never goes below 320 px during the drag, or a drag below that ends at 320 without a visible
      jump. — ⚠ NOT MET — split to TASK-198 (waits on Birko TASK-981). A drag past a minimum ends at it, but with an
      animated correction.
- [x] Dragon still keeps its own width when the window is resized (TASK-058's behaviour).

## Out of scope

- Touch dragging of tabs (TASK-058's notes)
- Deferred to TASK-198 — the divider stopping at the minimums while dragged (needs Birko TASK-981)

## Human test plan

- [x] On a throwaway server, drag the divider slowly with a mouse both ways: Dragon stretches with it and nothing
      jumps on release.
- [x] Resize the window after a drag: Dragon keeps its width.

## Implementation plan

Planned at pick (2026-11-25), after Birko fixed its side (Birko.Web main 0b3e085):

1. Red first, in `tests/ui-e2e/layouts.spec.ts`:
   - A mouse drag checks for no gap beside Dragon at every step, one Dragon width in every frame for 400 ms after the
     drop, the stored `{ 1fr, Npx }`, and the width after a reload.
   - A drag past the tabs' minimum ends at 320 px.
   - Red against Birko 0b3e085: 8 widths after the drop. The existing keyboard test was red too (1024 px instead of
     424).
2. `resized()` (`project-workspace.ts`) reads Dragon's width from `detail` px, falls back to an older panel's `master`
   px, and clamps it as before.
3. `dragon-focus.spec.ts` checks the panel's real transition (0.15s, and 0s under reduced motion) instead of the
   variable, and CLAUDE.md "Workspace layouts" describes the divider.

## Notes

- **The Birko side was Birko TASK-979** (`birko-framework-44`, Birko.Web main 0b3e085, part of v0.5.3, not pushed).
  - A panel with a flexible master and a fixed detail now resizes the detail.
  - Its drop emits `{ master: '1fr', detail: 'Npx' }`.
  - DraCode builds from Birko.Web's local sources, so this change needs that Birko.Web. Against an older one,
    `resized()` still reads the master px.
- **Human test on a throwaway server** (the TASK-163 copy of `greeter175`), with a mouse:
  - Drag right 500 px, then left 200 px: 0 px gap at every step, and the drop kept 564, then 764 px, with no
    movement.
  - A reload kept 764 px, and widening the window from 1440 to 1700 px left Dragon at 764.
  - Drags past the minimums ended at 320 px for the tabs and 280 px for Dragon, through an animated correction
    (TASK-198). No page errors.
- Birko's own human test of TASK-979 was run from here too: focus widens over about 170 ms, and reduced motion is
  instant (Birko closed it).
- Full e2e suite 186 passed (nothing on 57087); .NET suite 640 passed.

