---
id: FEATURE-028
created: 2026-05-31
---

# Async ask-human tool for automatic agents — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add a non-blocking `ask_human` tool, distinct from the interactive console prompt | approved | Background agents can't use a blocking console prompt | 2026-11-16 | owner | TASK-021 |
| D2 | Question carries prompt text, optional choice options, an allow-free-text flag, and context for why it's asking | approved | A structured question lets the human answer precisely and the agent validate the reply | 2026-11-16 | owner | TASK-021 |
| D3 | Calling the tool parks the task (reusing the waiting mechanism) and treats a question as distinct intent from an escalation | approved | Reuse the plumbing but keep "asking" separate from "failing" | 2026-11-16 | owner | TASK-021 |
| D4 | Available to the worker agent (Kobold) only; not to the analyzer (Wyvern, see D7) or the pre-analysis agent (Wyrm) | changed | Was: worker and analyzer. Parking works per task, and the analyzer runs on a whole project with no task to park, so its part waits for a project-level wait (D7) | 2026-11-16 | owner | TASK-021 |
| D5 | Choice answers are validated against the options; free-text passes through verbatim | approved | Guard against invalid replies while allowing open answers | 2026-11-16 | owner | TASK-021, TASK-022 |
| D6 | On resume, the answer is delivered back into the agent's context as a synthetic message clearly attributed to the human, before its next step | approved | The round-trip is what makes asking actually useful | 2026-11-16 | owner | TASK-022 |
| D7 | The analyzer agent (Wyvern) can ask a person too | deferred | Needs a project-level wait that can suspend and resume an analysis. Unblock: such a wait exists; filed as its own task | 2026-11-16 | owner | TASK-170 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-025; decisions seeded from the story's behaviour bullets and its two task files (TASK-021 the tool, TASK-022 answer delivery into context).
- 2026-11-16 — D1 proposed → approved (owner)
- 2026-11-16 — D2 proposed → approved (owner)
- 2026-11-16 — D3 proposed → approved (owner)
- 2026-11-16 — D4 proposed → changed (owner): Kobold only; the analyzer's part split out as D7, since parking works per task and the analyzer has no task
- 2026-11-16 — D5 proposed → approved (owner): an invalid choice is refused when submitted and the question stays open
- 2026-11-16 — D6 proposed → approved (owner)
- 2026-11-16 — D7 added as deferred (owner), filed as TASK-170
