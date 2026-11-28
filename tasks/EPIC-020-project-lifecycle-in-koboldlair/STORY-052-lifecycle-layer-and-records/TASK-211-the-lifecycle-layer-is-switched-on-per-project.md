---
id: TASK-211
parent: STORY-052
feature: FEATURE-078
# status — one of: todo, in-progress, verify (code done, sign-off pending), done, cancelled
status: done
priority: P3
# assignee — one of: agent (default), unassigned, human, or an installed agent type's name; ai is read as agent, never written
assignee: agent
created: 2026-11-25
# depends-on: task or story ids this task waits for — see the tasks skill's slicing.md § Where the edges live
depends-on: []
blocks: [TASK-212, TASK-213, TASK-214, TASK-215, TASK-216]
# findings: ids this task remediates — from a review/audit/harvest/drill pass, or from ordinary
# field use with no pass behind it at all. Prefixes: see /tasks intake
findings: []
pr: 96
github-issue: null
jira-key: null
---

# The lifecycle layer is switched on per project

## Context

FEATURE-078 D5. A per-project switch: on by default for new projects, off for existing ones until adopted. The
question chain does not follow it (D27).

Decisions: FEATURE-078 D5.

## Acceptance criteria

- [x] A project carries the switch; new projects start with it on (one Dragon creates: as the person answered), existing ones off
- [x] The switch can be turned on (and off) from Dragon and the web UI
- [x] Every lifecycle feature except the question chain checks it

## Out of scope

- The other FEATURE-078 decisions, each in its own task

## Human test plan

- [x] A new project starts with the layer on; an existing one is off until switched on

## Implementation plan

Decided with the owner 2026-11-28 (recommended answers unless noted):
- **Existing projects:** imported codebases and projects created before the switch start off.
- **What follows it:** the checker agents (D8), the person's escalation card (D9; off, the last failure fails with a fix
  task as before) and fix tasks for repeated failures and alerts (D14). The acceptance check's own promise checks keep
  their global setting; the question chain (D27) and bug fixes (TASK-231) apply everywhere.
- **Both ways:** on and off, nothing recorded is removed.
- **Web UI:** a new workspace Settings tab (the owner's choice over the Spec tab's header); not on a phone.
- **Added by the owner mid-task:** Dragon asks whether a new project uses it (D5 recorded as changed).

Steps:
1. Red: `ProjectLifecycleSwitchTests` (defaults, reload, an older table, an import, switching live through a Drake, the
   specification tool, Warden's tool), gate tests (checkers skipped, card replaced by a fix), a fix-task test, a REST test
   and `project-settings.spec.ts`.
2. `Project.LifecycleEnabled` / `ProjectEntity` column (+ `SqlProjectRepository` column repair), `RegisterProject(...,
   lifecycleEnabled = true)`, `SetLifecycleEnabledAsync`.
3. `Drake.LifecycleEnabled` (live, from `DrakeFactory`), gating `TaskChecks`, `EscalateAcceptanceAsync` and
   `FileFixTaskAsync` (except kind `acceptance`).
4. Dragon: `manage_specification` create requires `lifecycle_layer`, kept in the features sidecar and read at
   registration; Dragon's prompt asks before Sage creates; Sage never asks or writes it into the specification text;
   Warden's `lifecycle_layer` tool (status/on/off).
5. REST `PATCH /projects/{id}/lifecycle`, `lifecycleEnabled` in the project views and on `POST /projects`.
6. Web: `settings-tab.ts`, the Settings tab (desktop only); five e2e expectations of the tab list updated.
7. CLAUDE.md.

## Notes

- 2026-11-28: done. .NET suite 785 passed; web e2e 194 passed (one phone focus-ring test failed once under the full
  run and passed 4 of 4 alone, and 2 of 2 without this change: a timing flake).
- Human test, a throwaway server with the live model (Testing, run from a scratch folder, a copy of TASK-229's database):
  - The older project read as off; its table gained `LifecycleEnabled` on startup.
  - First run: Dragon asked only after Sage had already created the specification (registered on), and the person's
    "off" went to Sage, which wrote it into the specification text. Fixed: Dragon asks before Sage creates, and Sage
    never handles the switch.
  - Second run: Dragon decided "on, as I recommended" without asking. Fixed structurally: `manage_specification` create
    refuses without `lifecycle_layer`, and Dragon's prompt says the answer is the person's, even when told to go ahead.
  - Third run: Dragon asked (recommending yes) and created nothing; "no" created `tinyhello` off (`lifecycleLayer: false`
    in its sidecar); "switch it on for human229" went through Warden, and the older project read on.
