---
id: TASK-225
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-044]
pr: null
github-issue: null
jira-key: null
---

# Drake never loads the project's analysis, so Kobolds get no project constraints

## Context

Found 2026-11-25 in TASK-203's human test. `Drake.SetWyvern` (`DraCode.KoboldLair/Orchestrators/Drake.cs`) is the only
place that sets Drake's `_wyvern` and loads `_wyrmRecommendation` (`LoadWyrmRecommendationsAsync`), and nothing calls it.
So in the server a Drake never has Wyvern's analysis or Wyrm's recommendation in memory:

- When a Kobold is summoned, the combined constraints (`CombinedConstraints`: Wyrm's and Wyvern's constraints and
  out-of-scope items) are always empty, so `kobold.SetProjectConstraints` is never called and the Kobold never gets
  the "⛔ PROJECT CONSTRAINTS" block CLAUDE.md describes under *Constraints propagation*.
- Everything else that reads `_wyvern` or `_wyrmRecommendation` in Drake sees null too: the project structure handed to
  a Kobold (`_wyvern?.Analysis?.Structure`), the Wyrm info block (~L505–520), feature status updates through the
  Wyvern, and post-task verification steps from Wyrm's recommendations, if they read it there.

TASK-203 worked around it for the supervisor's sources only, by reading `analysis.json` and `wyrm-recommendation.json`
from the project folder (`Drake.AnalysisFacts`).

## Acceptance criteria

- [ ] A Drake in the server has the project's analysis and Wyrm recommendation (set when it is created, or read from the
      project folder), and a summoned Kobold gets the project's constraints; a test pins it, red today
- [ ] Each other reader of `_wyvern` / `_wyrmRecommendation` in Drake is checked, and what it was missing is listed in the
      task's notes
- [ ] The supervisor's sources (TASK-203) use the same loaded analysis rather than a second read, or the reason they keep
      their own is noted

## Out of scope

- Changing what the constraints say or how Kobolds use them

## Human test plan

- [ ] On a throwaway server, run a project whose spec has an explicit constraint: the Kobold's prompt (or its plan) shows
      the "PROJECT CONSTRAINTS" block with it

## Implementation plan

_Populated by `/tasks plan TASK-225` — leave empty until then._
