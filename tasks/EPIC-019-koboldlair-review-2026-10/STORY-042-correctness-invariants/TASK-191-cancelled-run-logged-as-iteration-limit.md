---
id: TASK-191
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-22
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-038]
pr: null
github-issue: null
jira-key: null
---

# A run stopped by a shutdown is logged as having reached its iteration limit

## Context

Found 2026-11-22 in TASK-121's human test. When the server stops mid-run, the Kobold saves its plan and its run ends
with unfinished steps. `Drake.ExecuteTaskAsync` (`DraCode.KoboldLair/Orchestrators/Drake.cs` ~2125) then reports
"⚠ Kobold … reached iteration limit - work incomplete (1/2 steps done)" for every run that ends with steps left,
whatever ended it. For a shutdown (or any cancelled run) that names the wrong reason, which misleads whoever reads the
log, and the message goes to the run's callback (and so to the run's live view) too.

## Acceptance criteria

- [ ] A run that ends because its cancellation token was cancelled (a shutdown, a park, a cancel) is reported as
      stopped, with that reason, not as having reached its iteration limit; a test pins it, red today
- [ ] A run that really used up its iterations still reports the iteration limit

## Out of scope

- What happens to the run on a shutdown (TASK-121, TASK-168)

## Human test plan

N/A — the message chosen for each ending is asserted by the test.

## Implementation plan

_Populated by `/tasks plan TASK-191` — leave empty until then._
