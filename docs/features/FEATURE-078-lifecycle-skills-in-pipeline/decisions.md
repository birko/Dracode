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

_`Date` and `By` stay `—` until `/feature decide` stamps a verdict — they record **when/who decided**, not when the row was created (creation is in the History log)._

**States:** `proposed` (fresh from grill, awaiting decision) · `approved` (build it) · `deferred` (not now — note unblock condition) · `changed` (approved but altered — record the delta) · `removed` (rejected / out of scope).

Only `approved` and `changed` rows generate tasks at `/feature decompose`. No row is terminal: a `deferred`/`removed` decision overturned by later evidence (incl. production feedback) is **reopened** by adding a *new* `proposed` row that links the superseded one — the old row is never deleted.

## History log

> Append-only. Every state change gets a dated line with the reason — this is the "why it changed", not just the current value.

- 2026-10-03 — feature created; decisions seeded as `proposed` from the design discussion and a short grill. The owner's stated direction during the grill: database is the source of truth and humans never edit tasks by hand (D3 over D16), opt-in per project (D5), gate failures go back to the worker (D9), checks as purpose-built agents (D10 over D17). D16 and D17 are recorded so they can be stamped `removed` at `/feature decide`.
