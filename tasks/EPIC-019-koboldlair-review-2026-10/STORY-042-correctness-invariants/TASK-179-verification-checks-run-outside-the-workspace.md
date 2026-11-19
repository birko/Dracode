---
id: TASK-179
parent: STORY-042
feature: null
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: todo
# blocked: <reason> — set by /tasks block, never by hand; status stays as it is; /tasks unblock removes it
priority: P2
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-19
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: []
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: [FIELD-029]
pr: null
github-issue: null
jira-key: null
---

# Verification checks run outside the code they check, so a fixed project fails verification forever

## Context

Found 2026-11-19 while running TASK-178's human test plan (Testing environment, scratch `projects025`, LAN `finstat`
model). With TASK-178's fix, project `waver`'s fix tasks loaded, ran and finished (syntax, test, lint), yet the next
verification failed all three checks again and added three new fix tasks, which ran and finished too. Every round
costs a full fix cycle of LLM work and never converges; the server was stopped after two rounds.

**Two causes, both in `WyvernVerificationService.ExecuteVerificationCheckAsync` / `ExecuteCommandAsync`:**

- **Working directory.** A check runs in `project.Paths.Output` (the project folder, e.g. `projects025/waver`) unless
  the step names a `WorkingDirectory`; the Kobolds write the code to `workspace/` under it (or to a feature worktree),
  so `node --check waver.js` reports the module missing (`node:internal/modules/cjs/loader … throw err`).
- **Shell.** On Windows a check runs through `powershell.exe`, but the steps (Wyrm's or the auto-detected ones) are
  written in POSIX shell: the lint check `grep -nE '\bwave\b' waver.js && exit 1 || exit 0` fails with a PowerShell
  parse error (`At line:1 char:32`), whatever the code says.

## Acceptance criteria

- [ ] A verification check runs where the project's code is (its workspace, or the external `SourcePath` for an
      imported project), shown by a test with a file only in `workspace/`
- [ ] A step written for a POSIX shell runs under one on Windows (Git Bash or `sh` when present), or the step's shell is
      explicit; a test shows `a && b || c` evaluated as POSIX
- [ ] A project whose fix tasks made the checks pass completes on the next verification
- [ ] The full suite passes

## Out of scope

- Which checks Wyrm recommends or auto-detection picks.
- Capping verification rounds (a project that cannot pass should still stop asking for fixes at some point; worth its
  own task if this one does not make it moot).

## Human test plan

- [ ] On a throwaway server, let a finished project fail verification, let its fix tasks run, and confirm the next
      verification passes and the project completes.

## Implementation plan

_Populated by `/tasks plan TASK-179` — leave empty until then._
