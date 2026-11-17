---
id: FEATURE-031
created: 2026-05-31
---

# End-to-end task acceptance verification — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Run a whole-task acceptance check after all steps complete, before marking the task done — and the same check when a task whose plan finished is recovered after a restart | approved | "All steps ran" must not masquerade as "task accomplished" | 2026-11-17 | owner | TASK-025 |
| D2 | Check the result against what the task itself promises — its target files and the interfaces it says it exports — not just "file exists and is non-empty" | changed | Was: against the recorded acceptance criteria, target files and promised interfaces. The feature's own criteria are not checked task by task; each task is held to what it was asked for | 2026-11-17 | owner | TASK-025 |
| D3 | On failure the task is not marked done: it is reopened with the unmet criteria, keeping its finished steps, up to two times; after that it is marked failed and a focused fix task names the unmet criteria | changed | Was: open a fix task at once. Most misses are small and the agent that did the work is best placed to finish it; the cap stops endless retries | 2026-11-17 | owner | TASK-025 |
| D4 | Criteria a machine can't judge always go to a person, who approves or rejects the task with a note; until they answer, the task waits and is not done, and its feature is not offered for merging | changed | Was: degrade to a human review instead of auto-passing. No fallback: a task is never marked done on a criterion nobody checked; a rejection takes the reopen path | 2026-11-17 | owner | TASK-025 |
| D5 | Ship behind one global switch, off by default until measured | approved | No throughput regression on healthy tasks until proven | 2026-11-17 | owner | TASK-025 |
| D6 | An LLM judges prose criteria first, so only the ones it cannot decide reach a human | deferred | Without it every task with prose criteria waits for a person. Unblock: the acceptance check (D1–D5) has run and shown how many tasks end up waiting on a person | 2026-11-17 | owner | TASK-165 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-028; decisions seeded from the story behaviour and TASK-025 acceptance criteria, all proposed pending /feature decide.
- 2026-11-16 — D6 proposed — TASK-165 spawned from TASK-025 (LLM judge for prose criteria, found while planning). TASK-025 now waits on EPIC-014 (owner, 2026-11-16): criterion 4's human check needs answerable notifications, and no fallback is wanted. Needs /feature decide.
- 2026-11-17 — D1 proposed → approved (owner): also covers a finished task recovered after a restart
- 2026-11-17 — D2 proposed → changed (owner): only what the task's own description promises
- 2026-11-17 — D3 proposed → changed (owner): reopen with the unmet criteria up to two times, then failed plus a fix task
- 2026-11-17 — D4 proposed → changed (owner): always a person, approve or reject; the task waits and its feature is not offered for merging meanwhile
- 2026-11-17 — D5 proposed → approved (owner): one global switch
- 2026-11-17 — D6 proposed → deferred (owner): until the check has run and shown how many tasks wait on a person; TASK-165 held
