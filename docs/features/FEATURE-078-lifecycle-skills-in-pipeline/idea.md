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
6. **Questions climb the hierarchy** (added 2026-11-25). An agent that cannot decide something
   asks the agent above it — a worker asks its supervisor (Drake), the supervisor asks the
   project's overseer (Wyvern), the overseer asks Dragon — and each answers only from what its
   level owns. Only what nobody on the way can answer reaches the person, in short rounds with a
   recommended answer each. Every answer an agent gives is recorded as a decision the person can
   see and overturn; what the person defines goes into the specification. Wyvern grows from a
   one-shot analyzer into the project's overseer that can look things up. This replaces today's
   path, where a worker's question goes straight to the person, and works in every project.
7. **Following the source.** These disciplines copy DraCode's own lifecycle skills. A scheduled
   check notices when a skill that KoboldLair mirrors changes, so KoboldLair can follow it.

## Open questions distilled from the grill

Q1–Q6 carry over the 2026-10-03 grill (answered then, before this table existed); Q7–Q9 are what it left open.
Q10–Q20 are the 2026-11-25 grill on the question hierarchy.

| id | question | type | blocked-by | state | claimed-by |
|----|----------|------|------------|-------|------------|
| Q1 | Where does the truth about tasks live? | decision | — | resolved — database only; files are a read-only rendering, and every change goes through the UI and Dragon (D3, D16) | — |
| Q2 | Is the lifecycle layer always on, or switched on per project? | decision | — | resolved — per project: on for new projects, off for existing ones until adopted (D5) | — |
| Q3 | What happens when the verify gate finds problems? | decision | — | resolved — back to the worker with the findings, escalated after a limit (D9) | — |
| Q4 | Are the checks the developer-tool instructions run as-is, or purpose-built agents? | decision | — | resolved — purpose-built KoboldLair agents (D10, D17) | — |
| Q5 | May people edit the rendered task files directly? | decision | Q1 | resolved — no; changes go through the UI and Dragon (D16) | — |
| Q6 | Does a single generic tool run the lifecycle instructions inside the pipeline? | decision | Q4 | resolved — no (D17) | — |
| Q7 | How many verify-gate retries before a task escalates? | decision | Q3 | resolved → D29 | — |
| Q8 | Does every check run on every task, or are checks chosen by task type? | decision | Q4 | resolved → D30 | — |
| Q9 | Are the current per-area task lists kept alongside the new layout during migration? | decision | Q1 | resolved → D31 | — |
| Q10 | Who answers a question at each level, and from what? | decision | — | resolved → D18 | — |
| Q11 | Does Dragon answer questions itself? | decision | Q10 | resolved → D19 | — |
| Q12 | Can the person see, and overturn, an answer an agent gave? | decision | Q10 | resolved → D21 | — |
| Q13 | How do questions reach the person? | decision | — | resolved → D22 | — |
| Q14 | Does a worker's ask-a-person question go through the chain instead of straight to the person? | decision | Q10 | resolved → D23 | — |
| Q15 | Does DraCode notice when a lifecycle skill that KoboldLair mirrors changes? | decision | — | resolved → D28 | — |
| Q16 | Does a definition the person gives go into the specification? | decision | Q11 | resolved → D20 | — |
| Q17 | Does Wyvern become a multi-turn overseer that can look things up? | decision | Q10 | resolved → D25 | — |
| Q18 | What stops a question bouncing between levels? | decision | Q10 | resolved → D24 | — |
| Q19 | How does the analyzer's own question (TASK-170) fit as the first slice? | decision | Q17 | resolved → D26 | — |
| Q20 | Does the question chain follow the per-project switch (D5), or work in every project? | decision | Q2, Q14 | resolved → D27 | — |

Fog:

- How the chain shows in the web UI and in Dragon while a question is with an agent, not yet with the person.

## Out of scope (initial)

- Humans editing task files directly and having the system pick the edits up (D16).
- A generic "run any developer skill" tool inside the pipeline (D17).
- Changing how DraCode's own development is tracked — it already uses this lifecycle.

## Prototype

Pending — headless pipeline behaviour; a prototype decision is taken at `/feature decide`.
