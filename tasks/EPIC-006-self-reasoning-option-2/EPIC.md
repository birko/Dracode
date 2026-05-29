---
id: EPIC-006
status: done
created: 2026-05-28
owner: ai
---

# Self-reasoning — Option 2 (structured tools)

## Area of concern

Option 1 (prompt-based self-reflection with CHECKPOINT blocks) is shipped. Option 2 adds explicit structured tools for reasoning + an external monitor service that analyzes Kobold outputs for stuck-loop patterns.

## Success criteria

- `ReflectionTool` forces explicit reasoning output (progress %, blockers, confidence, adjustment)
- Drake intervention triggered automatically when confidence < 30% or progress stalls 3+ checkpoints
- `ReasoningMonitorService` detects repeated error patterns + stuck loops independent of the Kobold's self-assessment
