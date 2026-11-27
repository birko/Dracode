---
id: FEATURE-078
created: 2026-10-03
---

# Project lifecycle discipline inside the KoboldLair pipeline — Decisions

> The decision ledger for stakeholders. Every idea-branch is a row with exactly one **state**. Rows are never deleted — `removed` is a state, not a deletion — so the ledger stays auditable.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Task states use the lifecycle vocabulary — to do · in progress · verify · done · cancelled — with "blocked" as a flag on a task rather than a state of its own | approved | The pipeline speaks the same task vocabulary as the lifecycle it copies | 2026-11-25 | owner | TASK-210 |
| D2 | Each project's plan is written out in the standard lifecycle layout: the project as an epic, each feature as a story, each unit of work as a task carrying acceptance criteria, dependencies, priority and a human test plan | approved | Each project gets the same readable plan DraCode keeps for itself | 2026-11-25 | owner | TASK-212 |
| D3 | The system's database stays the single source of truth; the lifecycle files are a one-way, read-only rendering of it, and every change goes through the KoboldLair UI or Dragon | approved | One source of truth; the files are only a view of it | 2026-11-25 | owner | TASK-212 |
| D4 | A new project starts with the full lifecycle layer (readme, project guide, features, tasks, changelog); an imported project gets only the parts it is missing | approved | New projects start complete; imported ones keep what they have | 2026-11-25 | owner | TASK-213 |
| D5 | The lifecycle layer is switched on per project — on by default for new projects, off for existing projects until they are adopted | approved | Existing projects are not changed under their owners | 2026-11-25 | owner | TASK-211 |
| D6 | Dragon's requirement interview records a decision ledger per feature (approved / deferred / changed / removed, with reasons), and only approved or changed decisions are broken into tasks | approved | It is where the answers of D19 and D21 are recorded | 2026-11-25 | owner | TASK-214 |
| D7 | Wyrm's pre-analysis records the project's conventions, vocabulary and hard-to-reverse choices (with their constraints and exclusions) as lasting project documents, not only as input to one run | approved | Later checks need recorded rules to check against | 2026-11-25 | owner | TASK-215 |
| D8 | A finished task is held before "done" by the existing acceptance check, extended so checker agents also confirm it built what was asked, follows the project's conventions, carries no stale comments and has no correctness problems; only then is it done and committed | changed | Was: a new verify gate. Now: built on the existing acceptance check, which already holds a finished task to its own description, and extended with checks for what was asked, the project's conventions, stale comments and correctness | 2026-11-25 | owner | TASK-217, TASK-218, TASK-219, TASK-220, TASK-221 |
| D9 | When a verify check fails, the findings go back to the worker for another attempt; after a set number of attempts the task escalates | approved | The worker fixes its own findings first; the limit is D29 | 2026-11-25 | owner | TASK-222 |
| D10 | The verify checks are purpose-built KoboldLair agents following the project's agent prompt conventions | approved | Each check is an agent written for its job, following the project's agent conventions | 2026-11-25 | owner | TASK-217 |
| D11 | When a feature completes, its capability specification is regenerated so the behaviour change can be reviewed before the feature is merged | deferred | Unblock: the verify gate (D8) ships, so a regenerated specification has checked work to describe | 2026-11-25 | owner | — |
| D12 | Merging a feature records its changes in the project changelog automatically | approved | A project's history is kept without anyone writing it by hand | 2026-11-25 | owner | TASK-216 |
| D13 | Dragon's progress and task views show the roadmap across features and tasks and flag where plan and work have drifted apart | deferred | Unblock: the plan is rendered in the standard layout (D2), which the roadmap view reads | 2026-11-25 | owner | — |
| D14 | Repeated failures and monitor alerts are filed as tracked fix tasks and worked through in order of impact | approved | Failures become tracked work instead of retries nobody sees | 2026-11-25 | owner | TASK-223 |
| D15 | Every acceptance criterion or specification scenario produces matching test work, cheapest test layer first | deferred | Unblock: the verify gate (D8) ships, so test work has criteria that are checked | 2026-11-25 | owner | — |
| D16 | Humans may edit the rendered task files directly and the system imports those edits back | removed | Every change goes through the UI and Dragon (D3); imported hand edits would be a second source of truth | 2026-11-25 | owner | — |
| D17 | A single generic tool runs the developer lifecycle instructions as-is inside the pipeline, instead of purpose-built agents | removed | The checks are purpose-built agents (D10), not developer instructions run as-is | 2026-11-25 | owner | — |
| D18 | Questions climb the hierarchy — worker → supervisor (Drake) → overseer (Wyvern) → Dragon → person — and each level answers only from what it owns: the supervisor from the task, its plan, the project's constraints and earlier answers; the overseer from the specification, its analysis and the decision ledger; anything else goes up | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-201, TASK-203, TASK-205 |
| D19 | Dragon answers only from recorded decisions and the specification's own text, never from its own judgement; when the specification does not cover something, Dragon asks the person to define it | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-206 |
| D20 | What the person defines is written into the specification as a new version, so the next question about it is answered from the specification | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-207 |
| D21 | Every answer an agent gives instead of the person is recorded as a decision (who answered, and why), shown to the person for information (a card in Dragon offering to keep it, and in Needs you, until kept or overturned), and can be overturned, which reopens the work it affected: on a task, the task reopens and its plan is revised with the person's answer, finished steps kept; on the project, the analysis runs again if no task has started, otherwise the answer is recorded for later runs and the person is told nothing finished was redone | changed | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-208 |
| D22 | Questions reach the person in rounds of at most five, each with a recommended answer and its one-line reason | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-209 |
| D23 | A worker's question for a person goes through the chain instead of straight to the person; its task waits, as it does today, while the question climbs | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-202 |
| D24 | A question only moves up, each level gets it once, and the person is the end of the line; the answer travels back down the same path to the agent that asked | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-201 |
| D25 | Wyvern becomes the project's overseer: an agent that works over several turns with read-only access to the specification, its analysis, the decision ledger, the task lists and the workspace, called both to analyze the project and to answer questions at its level | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-204 |
| D26 | First slice: the analyzer can ask questions — at most three in one round, listed in its analysis; the project's analysis waits while one is open (the open question itself holds it, no new project state), with no time limit; once answered, the analysis runs again with the answers. Its questions go to the person until the chain exists, then through Dragon's level | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-170 |
| D27 | The question chain works in every project, not only those with the lifecycle layer switched on (D5) | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-201 |
| D28 | A scheduled check in DraCode's GitHub CI compares the lifecycle skills KoboldLair mirrors — listed with the skills version each was last reviewed against — with the skills' latest version, and opens or updates one issue when a mirrored skill changed | approved | The owner's answer in the 2026-11-25 grill | 2026-11-25 | owner | TASK-224 |
| D29 | A task whose checks fail goes back to its worker at most twice; the third failure escalates (the same limit as today's acceptance check) | approved | Answers Q7; one limit for both gates | 2026-11-25 | owner | TASK-222 |
| D30 | Every check runs on every task, cheapest first | approved | Answers Q8; no task type slips past a check nobody thought to assign | 2026-11-25 | owner | TASK-217 |
| D31 | The current per-area task lists are kept alongside the new layout until the new layout is rendered, so the supervisor keeps working during the switch | approved | Answers Q9 | 2026-11-25 | owner | TASK-212 |

_`Date` and `By` stay `—` until `/feature decide` stamps a verdict — they record **when/who decided**, not when the row was created (creation is in the History log)._

**States:** `proposed` (fresh from grill, awaiting decision) · `approved` (build it) · `deferred` (not now — note unblock condition) · `changed` (approved but altered — record the delta) · `removed` (rejected / out of scope).

Only `approved` and `changed` rows generate tasks at `/feature decompose`. No row is terminal: a `deferred`/`removed` decision overturned by later evidence (incl. production feedback) is **reopened** by adding a *new* `proposed` row that links the superseded one — the old row is never deleted.

## History log

> Append-only. Every state change gets a dated line with the reason — this is the "why it changed", not just the current value.

- 2026-10-03 — feature created; decisions seeded as `proposed` from the design discussion and a short grill. The owner's stated direction during the grill: database is the source of truth and humans never edit tasks by hand (D3 over D16), opt-in per project (D5), gate failures go back to the worker (D9), checks as purpose-built agents (D10 over D17). D16 and D17 are recorded so they can be stamped `removed` at `/feature decide`.
- 2026-11-25 — D18–D28 added as `proposed` from a grill with the owner on how questions climb the agent hierarchy, Wyvern as the project's overseer, the analyzer's own questions (TASK-170, re-homed here from FEATURE-028 D7) as the first slice, and a CI check that follows changes to the mirrored skills. The owner's answers are the proposed rows; stamping is `/feature decide`.
- 2026-11-25 — D1–D7, D9, D10, D12, D14 proposed → approved (owner)
- 2026-11-25 — D8 proposed → changed (owner): built on the existing acceptance check and extended with the checker agents, instead of a new gate
- 2026-11-25 — D11, D15 proposed → deferred (owner): unblock when the verify gate (D8) ships; D13 proposed → deferred: unblock when the plan is rendered in the standard layout (D2)
- 2026-11-25 — D16, D17 proposed → removed (owner), as stated in the 2026-10-03 grill
- 2026-11-25 — D18–D28 proposed → approved (owner), as answered in the grill
- 2026-11-25 — D29–D31 added and approved (owner), answering Q7–Q9
- 2026-11-25 — D1 decomposed → TASK-210
- 2026-11-25 — D2 decomposed → TASK-212
- 2026-11-25 — D3 decomposed → TASK-212
- 2026-11-25 — D4 decomposed → TASK-213
- 2026-11-25 — D5 decomposed → TASK-211
- 2026-11-25 — D6 decomposed → TASK-214
- 2026-11-25 — D7 decomposed → TASK-215
- 2026-11-25 — D8 decomposed → TASK-217, TASK-218, TASK-219, TASK-220, TASK-221
- 2026-11-25 — D9 decomposed → TASK-222
- 2026-11-25 — D10 decomposed → TASK-217
- 2026-11-25 — D12 decomposed → TASK-216
- 2026-11-25 — D14 decomposed → TASK-223
- 2026-11-25 — D18 decomposed → TASK-201, TASK-203, TASK-205
- 2026-11-25 — D19 decomposed → TASK-206
- 2026-11-25 — D20 decomposed → TASK-207
- 2026-11-25 — D21 decomposed → TASK-208
- 2026-11-25 — D22 decomposed → TASK-209
- 2026-11-25 — D23 decomposed → TASK-202
- 2026-11-25 — D24 decomposed → TASK-201
- 2026-11-25 — D25 decomposed → TASK-204
- 2026-11-25 — D26 decomposed → TASK-170
- 2026-11-25 — D27 decomposed → TASK-201
- 2026-11-25 — D28 decomposed → TASK-224
- 2026-11-25 — D29 decomposed → TASK-222
- 2026-11-25 — D30 decomposed → TASK-217
- 2026-11-25 — D31 decomposed → TASK-212
- 2026-11-27 — D21 changed (detail, owner's answers while planning TASK-208): shown as a keep-or-overturn card in Dragon and in Needs you; an overturned task answer revises the task's plan (finished steps kept); an overturned project answer re-runs the analysis only while no task has started
