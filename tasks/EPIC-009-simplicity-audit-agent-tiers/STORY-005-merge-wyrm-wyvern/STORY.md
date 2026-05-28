---
id: STORY-005
parent: EPIC-009
status: planned
created: 2026-05-28
---

# Evaluate merging Wyrm + Wyvern into one analyzer

## User story

As a KoboldLair maintainer, I want to know whether `Wyrm` and `Wyvern` can be merged into a single analyzer agent so that I can decide if the pipeline complexity (and 2× LLM calls per project) is paying for real separation of concerns.

## Behaviour

- Both agents read the same spec and emit JSON. Wyrm outputs `wyrm-recommendation.json` (languages, agent types, tech stack, complexity, constraints, out-of-scope, verification steps). Wyvern outputs `analysis.json` (constraints, out-of-scope, requirements coverage, task breakdown with priorities and dependencies).
- The split is conceptual ("pre-analysis" vs "detailed analysis"), not technical. A single LLM call with a larger output schema could produce both.
- **A/B test plan**: run both pipelines (split vs merged) on 5-10 representative specs; score on:
  - Requirement coverage (does every spec requirement map to a task?)
  - Constraint propagation (do constraints reach Kobolds?)
  - Total LLM cost per spec
  - Wall-clock latency to `Analyzed` status
  - Subsequent Kobold escalation rate (proxy for analysis quality)
- **Risk**: Wyrm's `Constraints` are currently inputs to Wyvern's task descriptions. If merged, constraints and tasks are generated in one call — the model might fail to anchor task descriptions back to constraints. Test specifically for this.
- **Open question**: should the merged agent still emit `wyrm-recommendation.json` as a separate artifact for the Warden `view_analysis` tool, or fold it into `analysis.json`? Decide based on whether downstream consumers other than Wyvern actually read it.
