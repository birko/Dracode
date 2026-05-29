---
id: TASK-012
parent: STORY-003
status: done
priority: P2
assignee: ai
created: 2026-05-28
closed: 2026-05-29
depends-on: [TASK-011]
blocks: []
pr: null
github-issue: null
jira-key: null
---

# ReasoningMonitorService

> **Closed as done during `/tasks audit` (2026-05-29).** Already shipped via the 2026-03-15 reflection system — `DraCode.KoboldLair.Server/Services/ReasoningMonitorService.cs`, a `PeriodicBackgroundService` (45s) registered as a hosted service in `KoboldLair.Server/Program.cs`. All 6 ACs satisfied: detects stuck loops (`CheckStuckLoop`), stalled progress (`CheckStalledProgress`), repeated errors (`CheckRepeatedErrors`), budget exhaustion (`CheckBudgetExhaustion`); thresholds configurable via `KoboldLair:Reflection` in appsettings. Drake intervention is recommended by routing the alert through `Drake.HandleEscalationAsync` (which surfaces the Dragon notification), rather than the monitor emitting the notification directly.

## Context

External service analyzing Kobold outputs independent of the Kobold's self-assessment. Detects patterns the Kobold itself might not flag.

## Acceptance criteria

- [ ] Background service (`PeriodicBackgroundService` or `IJob` via `Birko.BackgroundJobs`)
- [ ] Periodic scan of recent Kobold output logs
- [ ] Detects:
  - Repeated error patterns (same exception type N+ times)
  - Stuck loops (same files modified repeatedly without progress)
  - Low-progress iterations (consistent < 5% progress delta)
- [ ] Recommends Drake intervention with structured reason
- [ ] Recommendations surfaced via Dragon notification
- [ ] Configurable thresholds via `appsettings.json`

## Out of scope

- ML-based pattern detection (heuristic rules in v1)
- Cross-Kobold pattern correlation (single-Kobold scope first)

## Implementation plan

_Populated by `/tasks plan TASK-012` — leave empty until then._
