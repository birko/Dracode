# Tasks — DraCode

_Generated 2026-05-28 by `/tasks import` from `TODO.md`. `IMPLEMENTATION_PLAN.md` was kept in place (mostly reference docs — Testing Strategy / Deployment / Maintenance / Risk Management). Last updated 2026-05-28 by `/tasks new` adding EPIC-009/010/011 (Anthropic-article gap epics). Run `/tasks triage` to refresh. **Do not hand-edit** — changes will be overwritten._

## Counts

| Status       | Epics | Stories | Tasks |
|--------------|-------|---------|-------|
| planned      | 11    | 13      | —     |
| todo         | —     | —       | 17    |
| in-progress  | 0     | 0       | 0     |
| blocked      | —     | —       | 0     |
| done         | 0     | 0       | 0     |
| cancelled    | 0     | 0       | 0     |

## In progress now

_None — start with `/tasks pick` or pick by reading the tree below._

## Tree

- **EPIC-001** [Token storage & auth providers](EPIC-001-token-storage-auth/EPIC.md) — planned (0/2)
  - [ ] [TASK-001](EPIC-001-token-storage-auth/TASK-001-encrypted-token-storage.md) Encrypted token storage (P1)
  - [ ] [TASK-002](EPIC-001-token-storage-auth/TASK-002-oauth-integration.md) OAuth integration (Google, GitHub) (P2)
- **EPIC-002** [Plugin & extensibility](EPIC-002-plugin-extensibility/EPIC.md) — planned (0/1)
  - [ ] [TASK-003](EPIC-002-plugin-extensibility/TASK-003-plugin-system.md) Plugin system for custom tools (P2)
- **EPIC-003** [Web UI polish](EPIC-003-web-ui-polish/EPIC.md) — planned (0/2)
  - [ ] [TASK-004](EPIC-003-web-ui-polish/TASK-004-drag-drop-tab-reorder.md) Drag-and-drop tab reordering (P2)
  - [ ] [TASK-005](EPIC-003-web-ui-polish/TASK-005-workspace-config-save-load.md) Save/load workspace configurations (P2)
- **EPIC-004** [Team & compliance](EPIC-004-team-compliance/EPIC.md) — planned (0/3)
  - STORY-001 [Enterprise features](EPIC-004-team-compliance/STORY-001-enterprise-features/STORY.md) — planned (0/3)
    - [ ] [TASK-006](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-006-team-collaboration.md) Team collaboration + RBAC (P1)
    - [ ] [TASK-007](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-007-audit-logging.md) Audit logging (P1)
    - [ ] [TASK-008](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-008-ci-cd-integration.md) CI/CD integration (P1)
- **EPIC-005** [Observability](EPIC-005-observability/EPIC.md) — planned (0/2)
  - STORY-002 [Monitoring infrastructure](EPIC-005-observability/STORY-002-monitoring-infra/STORY.md) — planned (0/2)
    - [ ] [TASK-009](EPIC-005-observability/STORY-002-monitoring-infra/TASK-009-prometheus-metrics.md) Prometheus /metrics endpoint (P2)
    - [ ] [TASK-010](EPIC-005-observability/STORY-002-monitoring-infra/TASK-010-health-dashboard.md) Health dashboard web UI (P2)
- **EPIC-006** [Self-reasoning Option 2](EPIC-006-self-reasoning-option-2/EPIC.md) — planned (0/2)
  - STORY-003 [Structured reasoning tools](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/STORY.md) — planned (0/2)
    - [ ] [TASK-011](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-011-reflection-tool.md) ReflectionTool (P2)
    - [ ] [TASK-012](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-012-reasoning-monitor-service.md) ReasoningMonitorService (P2)
- **EPIC-007** [Multi-platform clients](EPIC-007-multi-platform-clients/EPIC.md) — planned (0/3)
  - STORY-004 [Alternative clients](EPIC-007-multi-platform-clients/STORY-004-alternative-clients/STORY.md) — planned (0/3)
    - [ ] [TASK-013](EPIC-007-multi-platform-clients/STORY-004-alternative-clients/TASK-013-cli-client.md) KoboldLair CLI client (P2)
    - [ ] [TASK-014](EPIC-007-multi-platform-clients/STORY-004-alternative-clients/TASK-014-vscode-extension.md) VS Code extension (P2)
    - [ ] [TASK-015](EPIC-007-multi-platform-clients/STORY-004-alternative-clients/TASK-015-python-sdk.md) Python SDK (P2)
- **EPIC-008** [Research / advanced ML](EPIC-008-research-advanced-ml/EPIC.md) — planned (0/2)
  - [ ] [TASK-016](EPIC-008-research-advanced-ml/TASK-016-rag-codebase-understanding.md) RAG for codebase understanding (P2)
  - [ ] [TASK-017](EPIC-008-research-advanced-ml/TASK-017-fine-tuned-models.md) Fine-tuned models (P2)
- **EPIC-009** [Simplicity audit — collapse over-stacked agent tiers](EPIC-009-simplicity-audit-agent-tiers/EPIC.md) — planned (research/spike, no tasks yet)
  - STORY-005 [Evaluate merging Wyrm + Wyvern into one analyzer](EPIC-009-simplicity-audit-agent-tiers/STORY-005-merge-wyrm-wyvern/STORY.md) — planned
  - STORY-006 [Evaluate folding KoboldPlanner into Kobold](EPIC-009-simplicity-audit-agent-tiers/STORY-006-fold-koboldplanner-into-kobold/STORY.md) — planned
  - STORY-007 [Evaluate collapsing Drake periodic services](EPIC-009-simplicity-audit-agent-tiers/STORY-007-collapse-drake-services/STORY.md) — planned
- **EPIC-010** [Add voting parallelization to high-stakes decisions](EPIC-010-voting-parallelization/EPIC.md) — planned (research/spike, no tasks yet)
  - STORY-008 [Wyvern task breakdown voting](EPIC-010-voting-parallelization/STORY-008-wyvern-task-breakdown-voting/STORY.md) — planned
  - STORY-009 [WyrmAgent agent-type selection voting](EPIC-010-voting-parallelization/STORY-009-wyrmagent-selection-voting/STORY.md) — planned
  - STORY-010 [KoboldPlanner plan voting](EPIC-010-voting-parallelization/STORY-010-koboldplanner-plan-voting/STORY.md) — planned (blocked on STORY-006)
- **EPIC-011** [Tight evaluator-optimizer loop for Kobold actions](EPIC-011-tight-evaluator-optimizer/EPIC.md) — planned (research/spike, no tasks yet)
  - STORY-011 [Design EvaluatorAgent](EPIC-011-tight-evaluator-optimizer/STORY-011-design-evaluator-agent/STORY.md) — planned
  - STORY-012 [Integrate EvaluatorAgent into the Kobold tool loop](EPIC-011-tight-evaluator-optimizer/STORY-012-integrate-evaluator-into-kobold/STORY.md) — planned (blocked on STORY-011)
  - STORY-013 [Measure: does external evaluator catch failures the reflect tool misses?](EPIC-011-tight-evaluator-optimizer/STORY-013-measure-evaluator-vs-reflect/STORY.md) — planned (blocked on STORY-012)
