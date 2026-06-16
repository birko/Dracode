---
id: FEATURE-028
created: 2026-05-31
---

# Async ask-human tool for automatic agents — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Add a non-blocking `ask_human` tool, distinct from the interactive console prompt | proposed | Background agents can't use a blocking console prompt | — | — | TASK-021 |
| D2 | Question carries prompt text, optional choice options, an allow-free-text flag, and context for why it's asking | proposed | A structured question lets the human answer precisely and the agent validate the reply | — | — | TASK-021 |
| D3 | Calling the tool parks the task (reusing the waiting mechanism) and treats a question as distinct intent from an escalation | proposed | Reuse the plumbing but keep "asking" separate from "failing" | — | — | TASK-021 |
| D4 | Available to the worker and analyzer agents; not to the pre-analysis agent unless policy enables it | proposed | Pre-analysis should stay fully automatic | — | — | TASK-021 |
| D5 | Choice answers are validated against the options; free-text passes through verbatim | proposed | Guard against invalid replies while allowing open answers | — | — | TASK-021, TASK-022 |
| D6 | On resume, the answer is delivered back into the agent's context as a synthetic message clearly attributed to the human, before its next step | proposed | The round-trip is what makes asking actually useful | — | — | TASK-022 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-025; decisions seeded from the story's behaviour bullets and its two task files (TASK-021 the tool, TASK-022 answer delivery into context).
