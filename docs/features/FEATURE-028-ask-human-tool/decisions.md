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
| D8 | A person's answer also reaches every task that depends on the task that asked, directly or through other tasks, so the same question is not asked twice. Passed on: whatever a person wrote (ask_human answers, typed escalation instructions, approval remarks and rejection reasons); not timeouts or "handle it automatically". The dependent hears it as a person's answer to another task, named by that task, under the same decision tag | changed | Was: reaches the dependent tasks, scope unsaid. End-to-end run 2026-11-17: a dependent task found no record of the answer and asked the person again (D6 delivers to the asking task only). A chain must not lose the answer after one step; only what a person wrote tells a dependent anything; naming the other task keeps it from thinking it asked | 2026-11-23 | owner | TASK-175 |
| D9 | A typed answer resolves to an option when it names exactly one: its whole text in any letter case, the start of its text ending at a word ("Slovak" for "Slovak (Ahoj, <name>!)"), or its number as listed ("2"). The option's full text is stored. An answer naming two options is refused with the options listed, numbered; on a question that also allows free text, an answer naming none or two is kept as typed | changed | Was: names one option unambiguously, e.g. its leading word. End-to-end run 2026-11-17: "Slovak" was refused against "Slovak (Ahoj, <name>!)"; D5 did not say how close an answer must be, and text clients type answers | 2026-11-23 | owner | TASK-176 |

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
- 2026-11-17 — D8 added as proposed (agent), from an end-to-end run; filed as TASK-175 — needs `/feature decide`
- 2026-11-17 — D9 added as proposed (agent), from an end-to-end run; filed as TASK-176 — needs `/feature decide`
- 2026-11-23 — D8 proposed → changed (owner): every dependent, direct or indirect; anything a person wrote; named as the other task's answer
- 2026-11-23 — D9 proposed → changed (owner): any letter case, the start of one option at a word, or its number; the same on free-text questions
