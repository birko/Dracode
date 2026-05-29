---
id: EPIC-016
status: planned
created: 2026-05-29
owner: human
affects: []
---

# Database-backed agent working state

## Area of concern

Draw a hard architectural line between two kinds of data the agent pipeline produces:

- **Deliverable output** — what the user actually asked for: the generated code under `workspace/`, git-committed per feature branch. Stays as files. It *is* the product.
- **Agent working state** — everything the agents need to *do* their work but which is not the requested output: plans, task states, decisions, reasoning traces, reflections, recommendations, analysis, planning/coordination context, notifications. This belongs in the **database**, not as scattered `.md`/`.json` files in the per-project folder.

This continues the Birko.Data.SQL (PostgreSQL) migration already underway. Today the split is **partial**:

| Already in DB | Still file-based (candidates to move) |
|---|---|
| Plans (`SqlPlanRepository`) | `wyrm-recommendation.json` |
| Dragon history (`SqlHistoryRepository`) | `analysis.json` / `analysis.md` |
| Circuit-breaker state (`CircuitBreakerEntity`) | `planning-context.json` |
| Cost/usage (`SqlUsageRepository`) | `notifications.json` |
| Tasks (`TaskEntity`, with `.md` as a view) | `kobold-plans/*.md` (human-readable copies) |

The genuinely **new** part: **decisions and reasoning are not durably persisted as structured records anywhere**. They live in transient LLM turns and ephemeral `reflect` outputs. Persisting them as structured rows is what unlocks audit, cross-project learning (see `SharedPlanningContextService`), and — crucially — feeds the consensus/voting checks tracked in EPIC-010 and EPIC-011 (a verdict reads stored reasoning and writes back a new reasoning record).

Out of scope at the epic level:
- The generated code in `workspace/` and its git history — that is the deliverable and stays on disk.
- Human-readable `.md` exports (analysis.md, plan.md) — may remain as *generated views* of the DB record, not the source of truth.
- The consensus/voting mechanism itself — owned by EPIC-010 (voting) and EPIC-011 (evaluator-optimizer). This epic only provides the persisted reasoning/decision substrate they consume and emit.

## Success criteria

- No agent working-state file is the source of truth: every artifact in the "still file-based" column above is read from and written to the DB; any remaining file is a regenerable view, clearly labelled as such.
- A structured, queryable record exists for agent **decisions** and **reasoning/reflections**, linked to project + task + agent + (optional) consensus verdict.
- The deliverable-vs-working-state boundary is documented as an architectural rule (in CLAUDE.md) so future artifacts land in the right place by default.
- Removing a project's folder loses no working state (it's in the DB); only the regenerable views and the `workspace/` deliverable live on disk.
- Migration is backward-compatible: existing on-disk projects are imported into the DB on first load.
