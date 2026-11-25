---
id: TASK-199
parent: STORY-023
feature: FEATURE-026
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [Birko.Web/TASK-986]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# The phone tab bar's focus ring sits inside the tab again

## Context

Found 2026-11-25 by the `birko-framework-44` session after Birko TASK-983 (Birko.Web main 75a972f, v0.5.3) changed the
shared focus ring.

- `--b-focus-ring` is now two shadows: a 1px line and a soft glow (`0 0 0 1px <line>, 0 0 4px 1px <glow>`). Before,
  it was one 3px wash.
- `.phone-tab:focus-visible` in `src/app-shell.ts` (line ~177) draws `box-shadow: inset var(--b-focus-ring)`. The
  `inset` applies only to the first shadow, so the line is inside the tab and the glow outside it, spilling onto the
  neighbouring tab or clipped by the bar.
- Nothing is broken: the 1px line still has 3:1 contrast. It is the only inset use of the ring in Birko.Web and its
  consumers.
- The owner chose a Birko token over writing the shadows out here: Birko TASK-986 adds `--b-focus-ring-inset` (and
  `--b-focus-ring-danger-inset`) in every theme.

## Acceptance criteria

- [ ] `.phone-tab:focus-visible` uses `var(--b-focus-ring-inset)`, with no hand-written shadow.
- [ ] Focused with the keyboard at phone width, the whole ring (line and glow) is inside the tab in every theme.
- [ ] Nothing else in the client puts `inset` in front of `var(--b-focus-ring)`.

## Out of scope

- The other focus rings, which follow `--b-focus-ring` as they are.

## Human test plan

- [ ] At phone width on a throwaway server, Tab through the bottom bar in light and dark themes: each focused tab shows
      its ring inside it, nothing on the neighbours.
