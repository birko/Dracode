---
id: FEATURE-078
created: 2026-10-03
---

# Project lifecycle discipline inside the KoboldLair pipeline — Decisions

> The decision ledger for stakeholders. Every idea-branch is a row with exactly one **state**. Rows are never deleted — `removed` is a state, not a deletion — so the ledger stays auditable.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Task states use the lifecycle vocabulary — to do · in progress · verify · done · cancelled — with "blocked" as a flag on a task rather than a state of its own | proposed | — | — | — | — |
| D2 | Each project's plan is written out in the standard lifecycle layout: the project as an epic, each feature as a story, each unit of work as a task carrying acceptance criteria, dependencies, priority and a human test plan | proposed | — | — | — | — |
| D3 | The system's database stays the single source of truth; the lifecycle files are a one-way, read-only rendering of it, and every change goes through the KoboldLair UI or Dragon | proposed | — | — | — | — |
| D4 | A new project starts with the full lifecycle layer (readme, project guide, features, tasks, changelog); an imported project gets only the parts it is missing | proposed | — | — | — | — |
| D5 | The lifecycle layer is switched on per project — on by default for new projects, off for existing projects until they are adopted | proposed | — | — | — | — |
| D6 | Dragon's requirement interview records a decision ledger per feature (approved / deferred / changed / removed, with reasons), and only approved or changed decisions are broken into tasks | proposed | — | — | — | — |
| D7 | Wyrm's pre-analysis records the project's conventions, vocabulary and hard-to-reverse choices (with their constraints and exclusions) as lasting project documents, not only as input to one run | proposed | — | — | — | — |
| D8 | A finished task waits in *verify* until checks confirm it built what was asked, follows the project's conventions, carries no stale comments and has no correctness problems — only then is it done and committed | proposed | — | — | — | — |
| D9 | When a verify check fails, the findings go back to the worker for another attempt; after a set number of attempts the task escalates | proposed | — | — | — | — |
| D10 | The verify checks are purpose-built KoboldLair agents following the project's agent prompt conventions | proposed | — | — | — | — |
| D11 | When a feature completes, its capability specification is regenerated so the behaviour change can be reviewed before the feature is merged | proposed | — | — | — | — |
| D12 | Merging a feature records its changes in the project changelog automatically | proposed | — | — | — | — |
| D13 | Dragon's progress and task views show the roadmap across features and tasks and flag where plan and work have drifted apart | proposed | — | — | — | — |
| D14 | Repeated failures and monitor alerts are filed as tracked fix tasks and worked through in order of impact | proposed | — | — | — | — |
| D15 | Every acceptance criterion or specification scenario produces matching test work, cheapest test layer first | proposed | — | — | — | — |
| D16 | Humans may edit the rendered task files directly and the system imports those edits back | proposed | — | — | — | — |
| D17 | A single generic tool runs the developer lifecycle instructions as-is inside the pipeline, instead of purpose-built agents | proposed | — | — | — | — |
| D18 | Questions climb the hierarchy — worker → supervisor (Drake) → overseer (Wyvern) → Dragon → person — and each level answers only from what it owns: the supervisor from the task, its plan, the project's constraints and earlier answers; the overseer from the specification, its analysis and the decision ledger; anything else goes up | proposed | — | — | — | — |
| D19 | Dragon answers only from recorded decisions and the specification's own text, never from its own judgement; when the specification does not cover something, Dragon asks the person to define it | proposed | — | — | — | — |
| D20 | What the person defines is written into the specification as a new version, so the next question about it is answered from the specification | proposed | — | — | — | — |
| D21 | Every answer an agent gives instead of the person is recorded as a decision (who answered, and why), shown to the person for information, and can be overturned, which reopens the work it affected | proposed | — | — | — | — |
| D22 | Questions reach the person in rounds of at most five, each with a recommended answer and its one-line reason | proposed | — | — | — | — |
| D23 | A worker's question for a person goes through the chain instead of straight to the person; its task waits, as it does today, while the question climbs | proposed | — | — | — | — |
| D24 | A question only moves up, each level gets it once, and the person is the end of the line; the answer travels back down the same path to the agent that asked | proposed | — | — | — | — |
| D25 | Wyvern becomes the project's overseer: an agent that works over several turns with read-only access to the specification, its analysis, the decision ledger, the task lists and the workspace, called both to analyze the project and to answer questions at its level | proposed | — | — | — | — |
| D26 | First slice: the analyzer can ask questions — at most three in one round, listed in its analysis; the project's analysis waits while one is open (the open question itself holds it, no new project state), with no time limit; once answered, the analysis runs again with the answers. Its questions go to the person until the chain exists, then through Dragon's level | proposed | — | — | — | TASK-170 |
| D27 | The question chain works in every project, not only those with the lifecycle layer switched on (D5) | proposed | — | — | — | — |
| D28 | A scheduled check in DraCode's GitHub CI compares the lifecycle skills KoboldLair mirrors — listed with the skills version each was last reviewed against — with the skills' latest version, and opens or updates one issue when a mirrored skill changed | proposed | — | — | — | — |

_`Date` and `By` stay `—` until `/feature decide` stamps a verdict — they record **when/who decided**, not when the row was created (creation is in the History log)._

**States:** `proposed` (fresh from grill, awaiting decision) · `approved` (build it) · `deferred` (not now — note unblock condition) · `changed` (approved but altered — record the delta) · `removed` (rejected / out of scope).

Only `approved` and `changed` rows generate tasks at `/feature decompose`. No row is terminal: a `deferred`/`removed` decision overturned by later evidence (incl. production feedback) is **reopened** by adding a *new* `proposed` row that links the superseded one — the old row is never deleted.

## History log

> Append-only. Every state change gets a dated line with the reason — this is the "why it changed", not just the current value.

- 2026-10-03 — feature created; decisions seeded as `proposed` from the design discussion and a short grill. The owner's stated direction during the grill: database is the source of truth and humans never edit tasks by hand (D3 over D16), opt-in per project (D5), gate failures go back to the worker (D9), checks as purpose-built agents (D10 over D17). D16 and D17 are recorded so they can be stamped `removed` at `/feature decide`.
- 2026-11-25 — D18–D28 added as `proposed` from a grill with the owner on how questions climb the agent hierarchy, Wyvern as the project's overseer, the analyzer's own questions (TASK-170, re-homed here from FEATURE-028 D7) as the first slice, and a CI check that follows changes to the mirrored skills. The owner's answers are the proposed rows; stamping is `/feature decide`.
