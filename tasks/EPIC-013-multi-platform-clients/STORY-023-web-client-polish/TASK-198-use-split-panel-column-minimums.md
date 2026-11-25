---
id: TASK-198
parent: STORY-023
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
blocked: waits on Birko TASK-981 (b-split-panel master-min / detail-min), not yet in Birko.Web
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

- [ ] The workspace sets the panel's minimums from `MIN_TABS_PX` and `MIN_DRAGON_PX`. A mouse drag past either stops
      there while it is dragged, and nothing moves after the drop. `layouts.spec.ts`'s minimum test checks every frame
      after the drop.
- [ ] `resized()` keeps its clamp for a stored width that no longer fits (a narrower window).

## Out of scope

- The minimums themselves: Birko TASK-981

## Human test plan

- [ ] Drag the divider far both ways with a mouse: it stops at 320 px for the tabs and 280 px for Dragon, with no
      movement on release.

## Implementation plan

_Populated by `/tasks plan TASK-198` — leave empty until then._
