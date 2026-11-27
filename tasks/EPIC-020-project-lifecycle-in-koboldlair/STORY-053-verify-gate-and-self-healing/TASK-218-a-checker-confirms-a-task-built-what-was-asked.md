---
id: TASK-218
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-217]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: null
github-issue: null
jira-key: null
---

# A checker confirms a task built what was asked

## Context

FEATURE-078 D8. The first checker: did the task build what its description asked — nothing missing, nothing extra,
nothing built wrong (the lifecycle's verify-intent).

Decisions: FEATURE-078 D8.

## Acceptance criteria

- [x] The checker reports missing, extra and wrongly built requirements, each quoting the requirement
- [x] A task with no findings passes

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] A task that skips a stated requirement is held with a finding naming it

## Implementation plan

1. `IntentChecker` (`Services/Checks`, cost 10) on TASK-217's runner, after the lifecycle's verify-intent: the intent is
   the task's description plus the answers given for it (a person's answer is part of what was asked); the material is
   the files the task touched — its target paths (`TaskAcceptanceService.TargetPaths`, now public), its plan's files and
   what `git status` shows changed under the workspace — read with caps (12k chars a file, 48k in all), binary skipped,
   nothing outside the workspace.
2. `IntentCheckerAgent` on `CheckerAgent`: three kinds, `missing` (quote the requirement, name the file it belongs in),
   `wrong` (quote it, file and line, how it diverges), `extra` (file and line, what it does; supporting code is not
   extra); judged against the code, never what the task claims; what the files cannot settle is not a finding.
3. Drake passes the checkers the plan's files and the task's answered questions (`TaskCheckInput.PlannedFiles`,
   `Answers`). `Program.cs` registers the checker with the Kobold model, tracked as `intent-check`; it runs whenever the
   acceptance check is on (`KoboldLair:Verification:TaskAcceptanceEnabled`). No model configured: the check cannot run
   and the task goes to a person.

## Notes

- Red: the new tests could not compile without the checker; one real bug was red at first run — a planned "../x" path
  had its "../" trimmed before the outside-the-workspace check, so it would have been read. Fixed.
- Full .NET suite 742 passed.
- Human test, two parts on the real model (vLLM, the throwaway setup):
  - A scratch harness ran the checker on a menu task whose `menu.py` skipped "as its last line the opening hours
    'Mon-Fri 11:00-14:00'": one finding, `[intent] missing: The script never prints the opening hours …`, quoting the
    requirement and naming menu.py. The same task built in full: no findings.
  - A throwaway server with the acceptance check on ran a project (hours218) to the end: "Intent check on task 66246955:
    0 finding(s)", "Acceptance check passed (2 checks)", Done. The hold itself (a finding reopening the task) is shown by
    the tests, not live.
- Seen on the way: the Kobold printed Friday as 11:00-13:30, though the specification gave only "Mon: 11:00-14:00" as
  the style; the checker let it pass, which its rule 5 allows (the description sets no hours per day). A case for
  TASK-219's or a stricter reading later, not a defect here.
