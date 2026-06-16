---
id: FEATURE-030
created: 2026-05-31
---

# Answerable decisions surfaced in Dragon — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Notifications gain an optional "needs a response" flag plus a response schema (choice options / free-text shape) | proposed | A notification must carry enough to render an answer form | — | — | TASK-024 |
| D2 | A needs-response notification is pushed to the Dragon client live when a task parks or an agent asks | proposed | Stop making the human poll a notification list | — | — | TASK-024 |
| D3 | Dragon renders it as an actionable prompt (choice buttons / input), and submitting unparks and resumes the task | proposed | The whole point is acting in place, end to end | — | — | TASK-024 |
| D4 | A tool lets the human list and answer pending decisions explicitly, for missed live prompts | proposed | A live prompt can be missed; need an explicit fallback path | — | — | TASK-024 |
| D5 | Resolution is idempotent — answering an already-resolved decision is a no-op with a clear message | proposed | Prevent double-answering the same decision | — | — | TASK-024 |
| D6 | A decision raised while the human is offline persists and is replayed on reconnect | proposed | Consistent with existing notification persistence | — | — | TASK-024 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-027; decisions seeded from the story's behaviour bullets and its single task file (TASK-024 answerable notifications + Dragon round-trip).
