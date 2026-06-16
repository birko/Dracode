---
id: FEATURE-011
created: 2026-05-31
---

# WyrmAgent agent-type selection voting — Decisions

> Decision ledger for stakeholders. One state per row. Rows are never deleted.

## Decisions

| ID | Decision | State | Rationale | Date | By | → Tasks |
|----|----------|-------|-----------|------|----|---------|
| D1 | Make a single specialist-selection call first, as today | proposed | Most tasks have a clear technology; keep them at one call to avoid needless cost | — | — | — |
| D2 | Trigger extra voting only when the choice is uncertain (generalist fallback chosen, or 2+ technology mentions in the task) | proposed | Concentrate the extra cost where wrong assignments actually happen | — | — | — |
| D3 | When triggered, run 2 more selections in parallel and take a simple majority | proposed | Cheap single-call stage; majority is a robust low-cost vote | — | — | — |
| D4 | On a tie or three-way split, default to the generalist specialist | proposed | Guarantees the task is never blocked by an undecided vote | — | — | — |
| D5 | Gate behind `KoboldLair:Voting:Wyrm:Enabled` (default off) with a configurable ambiguity threshold | proposed | Allows A/B comparison and tuning without code changes | — | — | — |
| D6 | Open question: also count file-extension hints (e.g. `.tsx` → react) as an extra heuristic vote | proposed | Cheap signal, but mixing heuristic and model votes is unusual — needs a decision | — | — | — |

**States:** proposed · approved · deferred · changed · removed.
Only `approved` and `changed` generate tasks.

## History log
- 2026-05-31 — feature created; decisions seeded from STORY-009 behaviour and open question.
