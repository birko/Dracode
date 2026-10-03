---
id: FEATURE-078
created: 2026-10-03
owner: human
# status — one of: idea, review (built, sign-off pending), done, dropped, superseded
status: idea
---

# Project lifecycle discipline inside the KoboldLair pipeline

> Stakeholder-readable. A project manager or end user should understand the problem and the proposed shape without reading any code.

## Problem

DraCode itself is built with a disciplined project lifecycle: every idea becomes a feature
with a recorded set of decisions, approved decisions become tracked tasks with acceptance
criteria and a human test plan, finished work is checked against the request and against the
project's own rules before it counts as done, and specifications, the changelog and the
roadmap stay in step with the code.

The projects that KoboldLair builds for its users get none of this. The pipeline (Dragon →
Wyrm → Wyvern → Drake → Kobold) produces a specification, task lists in its own ad-hoc
format and generated code, but:

- there is no record of *which* ideas the user approved, deferred or rejected, and why;
- a task counts as "done" the moment the worker says so — nothing checks that it built what
  was asked, followed the project's conventions, or is correct;
- no living specification, changelog or roadmap is produced, so a user who comes back to a
  project weeks later has to rediscover what it does and how far it got;
- repeated failures are retried, but never turned into tracked fix work.

The cost of doing nothing: generated projects are harder to trust, harder to review and harder
to hand over to a human team.

## Proposed shape

Give every KoboldLair project (opt-in per project) the same lifecycle layer DraCode uses for
itself, wired into the stages that already exist:

1. **Same vocabulary and the same written record.** Task states use the lifecycle names
   (to do · in progress · verify · done · cancelled, with "blocked" as a flag), and the
   project folder carries the lifecycle files — features, decisions, tasks, changelog,
   project guide — in the standard layout. The system's database stays the single source of
   truth; all changes go through the KoboldLair UI and Dragon, and the files are a read-only
   rendering that any lifecycle-aware tool can read.
2. **Decisions, not just requirements.** Dragon's requirement interview produces a decision
   ledger per feature (approved / deferred / changed / removed, with reasons); only approved
   decisions are broken into tasks. Wyrm records the project's conventions, vocabulary and
   hard-to-reverse choices so later checks have rules to check against.
3. **A verify gate before "done".** A finished task waits in *verify* while dedicated checker
   agents confirm it built what was asked, follows the project's conventions, carries no stale
   comments and has no correctness problems. Findings go back to the worker; repeated failure
   escalates.
4. **Living documents.** When a feature completes, its specification is regenerated so the
   change can be reviewed before merge; merging rolls the changelog; Dragon's progress views
   show the roadmap and flag when plan and work drift apart.
5. **Self-healing.** Repeated failures and monitor alerts are filed as tracked fix tasks and
   worked through by impact.

## Open questions distilled from the grill

- Where does the truth about tasks live? → Database only; files are a read-only rendering,
  humans never edit tasks by hand — everything goes through the UI and Dragon (D3, D16).
- Always on, or per project? → Opt-in per project; on for new projects, off for existing ones
  until adopted (D5).
- What happens when the verify gate finds problems? → Back to the worker with the findings;
  escalate after a limit (D9).
- How are the checks built — reuse the developer-tool instructions as-is, or purpose-built
  agents? → Purpose-built KoboldLair agents (D10, D17).
- Still open: how many gate retries before escalation; whether every check runs on every task
  or is chosen by task type; whether the current per-area task lists are kept alongside the new
  layout during migration.

## Out of scope (initial)

- Humans editing task files directly and having the system pick the edits up (D16).
- A generic "run any developer skill" tool inside the pipeline (D17).
- Changing how DraCode's own development is tracked — it already uses this lifecycle.

## Prototype

Pending — headless pipeline behaviour; a prototype decision is taken at `/feature decide`.
