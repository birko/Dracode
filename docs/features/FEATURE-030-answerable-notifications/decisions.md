---
id: FEATURE-030
created: 2026-05-31
---

# Answerable decisions surfaced in Dragon — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Notifications gain an optional "needs a response" flag plus a response schema (choice options / free-text shape) | approved | A notification must carry enough to render an answer form | 2026-11-17 | owner | TASK-024 |
| D2 | A needs-response notification is pushed live to every Dragon session whose user can see the project, when a task parks or an agent asks | changed | Was: pushed to the Dragon client (in practice only a session viewing that project). A question on one project must not wait until the person happens to open it | 2026-11-17 | owner | TASK-024 |
| D3 | Answering a decision through Dragon unparks and resumes the task; the clickable card (choice buttons / input) is built in the rebuilt Dragon dock, and until then a person answers in the Dragon chat | changed | Was: Dragon renders an actionable prompt and submitting resumes the task. Today's web Dragon view is a placeholder that is being rebuilt, so the answer channel is built now and the card with the new dock | 2026-11-17 | owner | TASK-024, TASK-160 |
| D4 | A tool lets the human list and answer pending decisions explicitly, for missed live prompts, limited to the projects they can see | approved | A live prompt can be missed; need an explicit fallback path | 2026-11-17 | owner | TASK-024 |
| D5 | Resolution is idempotent — answering an already-resolved decision is a no-op with a clear message | approved | Prevent double-answering the same decision | 2026-11-17 | owner | TASK-024 |
| D6 | An open decision is shown again on every reconnect until it is answered or times out, whatever happened to its notification | changed | Was: a decision raised while offline is replayed on reconnect. Today a card replays once and can be dismissed while the decision is still open; the stored decision is what counts | 2026-11-17 | owner | TASK-024 |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created from STORY-027; decisions seeded from the story's behaviour bullets and its single task file (TASK-024 answerable notifications + Dragon round-trip).
- 2026-11-17 — D1 proposed → approved (owner)
- 2026-11-17 — D2 proposed → changed (owner): pushed to every session that can see the project, not only one viewing it
- 2026-11-17 — D3 proposed → changed (owner): the answer channel is built now (TASK-024); the clickable card moves to the rebuilt Dragon dock (TASK-160)
- 2026-11-17 — D4 proposed → approved (owner): limited to the projects the person can see
- 2026-11-17 — D5 proposed → approved (owner)
- 2026-11-17 — D6 proposed → changed (owner): replayed from the stored decision until answered or timed out
