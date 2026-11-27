---
id: TASK-220
parent: STORY-053
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: [TASK-217]
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: 90
github-issue: null
jira-key: null
---

# A checker finds stale or redundant comments

## Context

FEATURE-078 D8. Finds comments whose content lives elsewhere or no longer matches the code (the lifecycle's
review-comments).

Decisions: FEATURE-078 D8.

## Acceptance criteria

- [x] The checker reports each stale or redundant comment with where its content already lives

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] A comment that contradicts the code is held with a finding

## Implementation plan

1. `TaskMaterial.ChangeAsync`: the "git diff of the task's edits, then the touched files" material, moved out of
   `CorrectnessChecker` so both code-reading checkers share it.
2. `CommentChecker : ITaskChecker` (name "comments", cost 30: last, since a comment matters least and the run stops at
   the first checker with findings; no model → throws, so the task goes to a person).
3. `CommentCheckerAgent : CheckerAgent`: judges only comments the change wrote (diff lines, every comment in a new file),
   whole blocks. `stale` contradicts the code (quote, file and line, what the code does); `redundant` carries what lives
   elsewhere — the code beside it, the task or specification, git history — naming it after "lives in:". Not findings:
   a "why" comment, API documentation, a licence header, length alone.
4. `Program.cs`: registered after the correctness checker, the Kobold model tracked as `comment-check`.
5. Found in the human test: the intent checker reported the wrong comment itself, as "extra". Its rule 6 now says
   comments are not behaviour and another check judges them (asserted in `IntentCheckerTests`).

## Notes

- Red first: 3 tests in `CommentCheckerTests` (stale and redundant findings with where the content lives, the prompt's
  rules and the change's diff in the message; runs after correctness and an honest change passes; no model throws),
  and the intent checker's prompt test now asserts it leaves comments alone. Full suite: 755 passed.
- Human test (2026-11-27), scratch harness with a real Drake and all three checkers on the live model (vLLM `finstat`):
  a git workspace whose edit adds a correct `retry()` under the comment "Tries fn at most 3 times, then returns None"
  (the code tries `attempts` times and re-raises).
  - First run: the correctness checker held it first, for a real edge case in my test code (`retry(fn, 0)` raises
    `None`). Guarded it.
  - Second run: the intent checker held it, reporting the comment as `extra`. Added its rule 6.
  - Third run: intent and correctness passed; the comment checker held the task (attempt 1) with `[comments] stale`,
    quoting the comment at `retry.py` line 3 and what the code does. The same code with an honest "why" comment got 0
    findings.
- One full-suite run failed `EventStoreMigrationTests.Two_writers_changing_one_specification_at_once_both_land_on_distinct_versions`
  (untouched by this change); it passed three times alone and in the next full run. A concurrency test that can flake;
  TASK-228 moves the event store and runs its two-writers test on MongoDB.
