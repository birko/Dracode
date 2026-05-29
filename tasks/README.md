# Tasks — DraCode

_Generated 2026-05-29 by `/tasks triage`. Run `/tasks triage` to refresh. **Do not hand-edit** — changes will be overwritten._

## Counts

| Status       | Epics | Stories | Tasks |
|--------------|-------|---------|-------|
| planned      | 14    | 35      | —     |
| todo         | —     | —       | 20    |
| in-progress  | 0     | 0       | 0     |
| blocked      | —     | —       | 0     |
| done         | 1     | 1       | 3     |
| cancelled    | 0     | 0       | 0     |

## In progress now

_None — start with `/tasks pick` or pick by reading the tree below._

## Tree

- **EPIC-001** [Token storage & auth providers](EPIC-001-token-storage-auth/EPIC.md) — planned (0/2)
  - [ ] [TASK-001](EPIC-001-token-storage-auth/TASK-001-encrypted-token-storage.md) Encrypted token storage (P1)
  - [ ] [TASK-002](EPIC-001-token-storage-auth/TASK-002-oauth-integration.md) OAuth integration (Google, GitHub) (P2)
- **EPIC-002** [Plugin & extensibility](EPIC-002-plugin-extensibility/EPIC.md) — planned (0/1)
  - [ ] [TASK-003](EPIC-002-plugin-extensibility/TASK-003-plugin-system.md) Plugin system for custom tools (P2)
- **EPIC-004** [Team & compliance](EPIC-004-team-compliance/EPIC.md) — planned (0/3)
  - STORY-001 [Enterprise features](EPIC-004-team-compliance/STORY-001-enterprise-features/STORY.md) — planned (0/3)
    - [ ] [TASK-006](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-006-team-collaboration.md) Team collaboration + RBAC (P1)
    - [ ] [TASK-007](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-007-audit-logging.md) Audit logging (P1)
    - [ ] [TASK-008](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-008-ci-cd-integration.md) CI/CD integration (P1)
- **EPIC-005** [Observability](EPIC-005-observability/EPIC.md) — planned (0/2)
  - STORY-002 [Monitoring infrastructure](EPIC-005-observability/STORY-002-monitoring-infra/STORY.md) — planned (0/2)
    - [ ] [TASK-009](EPIC-005-observability/STORY-002-monitoring-infra/TASK-009-prometheus-metrics.md) Prometheus /metrics endpoint (P2)
    - [ ] [TASK-010](EPIC-005-observability/STORY-002-monitoring-infra/TASK-010-health-dashboard.md) Health dashboard web UI (P2)
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
- **EPIC-012** [Backend consolidation — unify on KoboldLair.Server](EPIC-012-backend-consolidation/EPIC.md) — planned (supersedes parts of EPIC-003 + EPIC-007; 1/1 tasks done)
  - STORY-014 [Retire DraCode.WebSocket and DraCode.Web](EPIC-012-backend-consolidation/STORY-014-retire-old-stack/STORY.md) — planned
  - STORY-015 [/kobold endpoint — ad-hoc + project-scoped Kobold execution](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/STORY.md) — planned
  - STORY-016 [REST + SSE facade for non-streaming clients](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/STORY.md) — planned
  - STORY-017 [OAuth/OIDC authentication and per-caller identity](EPIC-012-backend-consolidation/STORY-017-oauth-identity/STORY.md) — planned
  - STORY-018 [Daemon mode for KoboldLair.Server](EPIC-012-backend-consolidation/STORY-018-daemon-mode/STORY.md) — planned
  - [x] [TASK-018](EPIC-012-backend-consolidation/TASK-018-remove-sync-tool-execute.md) Remove sync `Tool.Execute()` overloads — keep only `ExecuteAsync` (P2)
- **EPIC-013** [Multi-platform clients on the unified backend](EPIC-013-multi-platform-clients/EPIC.md) — planned (depends on EPIC-012; supersedes EPIC-007)
  - STORY-019 [DraCode.KoboldLair.Cli — single-file binary CLI client](EPIC-013-multi-platform-clients/STORY-019-cli-client/STORY.md) — planned
  - STORY-020 [Discord bot client](EPIC-013-multi-platform-clients/STORY-020-discord-bot/STORY.md) — planned
  - STORY-021 [VSCode extension client](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/STORY.md) — planned
  - STORY-022 [Python SDK](EPIC-013-multi-platform-clients/STORY-022-python-sdk/STORY.md) — planned
  - STORY-023 [Web client polish (reclaimed from old EPIC-003)](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/STORY.md) — planned
- **EPIC-014** [Human-in-the-loop decision gates](EPIC-014-human-in-the-loop-gates/EPIC.md) — planned (0/6)
  - STORY-024 [Blocking "AwaitingHumanDecision" escalation tier](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/STORY.md) — planned (0/2)
    - [ ] [TASK-019](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/TASK-019-awaiting-human-decision-state.md) AwaitingHumanDecision state + HumanDecisionRequired disposition (P1)
    - [ ] [TASK-020](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/TASK-020-drake-parks-routes-to-human.md) Drake parks task & routes decision to human (P1)
  - STORY-025 [Async `ask_human` tool for automatic agents](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/STORY.md) — planned (0/2)
    - [ ] [TASK-021](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-021-async-ask-human-tool.md) Implement async `ask_human` tool (P1)
    - [ ] [TASK-022](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-022-deliver-answer-into-context.md) Deliver human answer back into agent context (P2)
  - STORY-026 [Policy-driven decision gates (per-project)](EPIC-014-human-in-the-loop-gates/STORY-026-decision-gate-policy/STORY.md) — planned (0/1)
    - [ ] [TASK-023](EPIC-014-human-in-the-loop-gates/STORY-026-decision-gate-policy/TASK-023-decision-gate-policy-config.md) DecisionGatePolicy config + enforcement (P2)
  - STORY-027 [Answerable decisions surfaced in Dragon](EPIC-014-human-in-the-loop-gates/STORY-027-answerable-notifications/STORY.md) — planned (0/1)
    - [ ] [TASK-024](EPIC-014-human-in-the-loop-gates/STORY-027-answerable-notifications/TASK-024-answerable-notifications-dragon.md) Answerable notifications + Dragon round-trip (P2)
- **EPIC-015** [Self-reasoning depth & end-to-end verification](EPIC-015-self-reasoning-depth/EPIC.md) — planned (0/4)
  - STORY-028 [End-to-end task acceptance verification](EPIC-015-self-reasoning-depth/STORY-028-end-to-end-task-verification/STORY.md) — planned (0/1)
    - [ ] [TASK-025](EPIC-015-self-reasoning-depth/STORY-028-end-to-end-task-verification/TASK-025-task-acceptance-check.md) Post-plan task-acceptance check (P1)
  - STORY-029 [Escalation loop closure — feed resolution back to the Kobold](EPIC-015-self-reasoning-depth/STORY-029-escalation-loop-closure/STORY.md) — planned (0/1)
    - [ ] [TASK-026](EPIC-015-self-reasoning-depth/STORY-029-escalation-loop-closure/TASK-026-feed-resolution-to-kobold.md) Feed escalation resolution back into Kobold context (P2)
  - STORY-030 [Budget- and cost-aware reflection](EPIC-015-self-reasoning-depth/STORY-030-budget-aware-reflection/STORY.md) — planned (0/1)
    - [ ] [TASK-027](EPIC-015-self-reasoning-depth/STORY-030-budget-aware-reflection/TASK-027-budget-aware-reflection.md) Surface real token/cost spend into reflect & monitor (P2)
  - STORY-031 [Cross-step coherence re-plan check](EPIC-015-self-reasoning-depth/STORY-031-cross-step-replan-check/STORY.md) — planned (0/1)
    - [ ] [TASK-028](EPIC-015-self-reasoning-depth/STORY-031-cross-step-replan-check/TASK-028-cross-step-coherence-check.md) Lightweight re-plan check when a step is revised/failed (P2)
- **EPIC-016** [Database-backed agent working state](EPIC-016-db-backed-agent-state/EPIC.md) — planned (continues Birko.Data.SQL migration; feeds EPIC-010/011)
  - STORY-032 [Migrate remaining file-based working artifacts to the database](EPIC-016-db-backed-agent-state/STORY-032-migrate-remaining-artifacts/STORY.md) — planned
  - STORY-033 [Persist structured decision & reasoning records](EPIC-016-db-backed-agent-state/STORY-033-persist-decisions-reasoning/STORY.md) — planned (substrate for EPIC-010/011)
  - STORY-034 [Codify and enforce the deliverable-vs-working-state boundary](EPIC-016-db-backed-agent-state/STORY-034-codify-deliverable-boundary/STORY.md) — planned
- **EPIC-017** [Generalized agent consensus](EPIC-017-generalized-agent-consensus/EPIC.md) — planned (generalizes EPIC-010/011 point-integrations)
  - STORY-035 [Reusable ConsensusService abstraction](EPIC-017-generalized-agent-consensus/STORY-035-reusable-consensus-mechanism/STORY.md) — planned
  - STORY-036 [Diverse-panel consensus (heterogeneous agents)](EPIC-017-generalized-agent-consensus/STORY-036-diverse-panel-consensus/STORY.md) — planned
  - STORY-037 [Retrofit EPIC-010 voting integrations onto the shared mechanism](EPIC-017-generalized-agent-consensus/STORY-037-retrofit-voting-integrations/STORY.md) — planned (depends on STORY-035)

<details>
<summary>✅ Completed (1 epic)</summary>

- **EPIC-006** [Self-reasoning Option 2](EPIC-006-self-reasoning-option-2/EPIC.md) — done (delivered via the 2026-03-15 reflection system; closed during `/tasks audit` 2026-05-29)
  - STORY-003 [Structured reasoning tools](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/STORY.md) — done (2/2)
    - [x] [TASK-011](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-011-reflection-tool.md) ReflectionTool (P2)
    - [x] [TASK-012](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-012-reasoning-monitor-service.md) ReasoningMonitorService (P2)

</details>
