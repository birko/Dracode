---
id: STORY-013
parent: EPIC-011
status: planned
created: 2026-05-28
---

# Measure: does external evaluator catch failures the reflect tool misses?

## User story

As a KoboldLair maintainer, I want to compare external EvaluatorAgent verdicts against historical Kobold reflect outputs and actual task outcomes, so I can make an evidence-based decision about whether to ship the evaluator.

## Behaviour

- **Data set**: replay 50-100 historical tasks across these buckets:
  - Tasks that succeeded (Done + commit + downstream tasks didn't escalate)
  - Tasks that failed with commit-failure (Bug B/C class from the recent commit-path fix)
  - Tasks that triggered `wrong_approach` escalation
  - Tasks that the Kobold claimed Done but a human flagged as wrong
- **Replay setup**: re-run the Kobold's tool trace; this time call EvaluatorAgent after each tool. Capture all evaluator verdicts.
- **Metrics**:
  - **True positives**: evaluator rejected AND actual outcome was failure → evaluator caught what reflect didn't
  - **False positives**: evaluator rejected but actual outcome was success → evaluator would have caused unnecessary thrash
  - **True negatives**: evaluator approved + actual success
  - **False negatives**: evaluator approved + actual failure → evaluator missed it
- **Comparison**: same metrics for the existing reflect tool's confidence scores. Does the evaluator add detection coverage on top of reflect, or just replicate it?
- **Decision criteria**:
  - True-positive rate > 70% AND false-positive rate < 20% → enable for `critical` tasks
  - False-positive rate ≥ 30% → drop the evaluator approach
  - Anything in between → STORY-011 calibration revisit (refine the rubric)
- **Output**: a decision memo + the replay dataset, attached to this story for future re-evaluation when the evaluator design changes.
- **Blocker**: STORY-011 + STORY-012 must ship first.
