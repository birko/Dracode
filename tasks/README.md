# Tasks — DraCode

_Generated 2026-06-16. Run `/tasks triage` to refresh. **Do not hand-edit** — changes will be overwritten._

## Counts

| Status       | Epics | Stories | Tasks |
|--------------|-------|---------|-------|
| planned      | 13    | 33      | —     |
| todo         | —     | —       | 59    |
| in-progress  | 1     | 1       | 0     |
| blocked      | —     | —       | 0     |
| done         | 1     | 2       | 6     |
| cancelled    | 0     | 0       | 0     |

## In progress now

_None_

## Tree

- **EPIC-001 Token storage & auth providers** — planned (0/2)
  - [ ] [TASK-001](EPIC-001-token-storage-auth/TASK-001-encrypted-token-storage.md) Encrypted token storage (P1)
  - [ ] [TASK-002](EPIC-001-token-storage-auth/TASK-002-oauth-integration.md) OAuth integration (Google, GitHub) (P2)
- **EPIC-002 Plugin & extensibility** — planned (0/1)
  - [ ] [TASK-003](EPIC-002-plugin-extensibility/TASK-003-plugin-system.md) Plugin system for custom tools (P2)
- **EPIC-004 Team & compliance** — planned (0/3)
  - STORY-001 Enterprise features — planned (0/3)
    - [ ] [TASK-006](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-006-team-collaboration.md) Team collaboration (RBAC + shared workspaces) (P1)
    - [ ] [TASK-007](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-007-audit-logging.md) Audit logging (P1)
    - [ ] [TASK-008](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-008-ci-cd-integration.md) CI/CD integration (GitHub Actions + Docker) (P1)
- **EPIC-005 Observability — Metrics + Health Dashboard** — planned (0/2)
  - STORY-002 Monitoring infrastructure — planned (0/2)
    - [ ] [TASK-009](EPIC-005-observability/STORY-002-monitoring-infra/TASK-009-prometheus-metrics.md) Prometheus /metrics endpoint (P2)
    - [ ] [TASK-010](EPIC-005-observability/STORY-002-monitoring-infra/TASK-010-health-dashboard.md) Health dashboard web UI (P2)
- **EPIC-008 Research / advanced ML** — planned (0/2)
  - [ ] [TASK-016](EPIC-008-research-advanced-ml/TASK-016-rag-codebase-understanding.md) RAG for codebase understanding (P2)
  - [ ] [TASK-017](EPIC-008-research-advanced-ml/TASK-017-fine-tuned-models.md) Fine-tuned models on project codebases (P2)
- **EPIC-009 Simplicity audit — collapse over-stacked agent tiers** — planned (0/0)
  - STORY-005 Evaluate merging Wyrm + Wyvern into one analyzer — planned (0/0)
  - STORY-006 Evaluate folding KoboldPlanner into Kobold — planned (0/0)
  - STORY-007 Evaluate collapsing the three Drake services — planned (0/0)
- **EPIC-010 Add voting parallelization to high-stakes decisions** — planned (0/0)
  - STORY-008 Wyvern task breakdown voting — planned (0/0)
  - STORY-009 WyrmAgent agent-type selection voting — planned (0/0)
  - STORY-010 KoboldPlanner plan voting — planned (0/0)
- **EPIC-011 Tight evaluator-optimizer loop for Kobold actions** — planned (0/0)
  - STORY-011 Design EvaluatorAgent — planned (0/0)
  - STORY-012 Integrate EvaluatorAgent into the Kobold tool loop — planned (0/0)
  - STORY-013 Measure: external evaluator vs reflect tool — planned (0/0)
- **EPIC-012 Backend consolidation — unify on KoboldLair.Server** — in-progress (4/23)
  - [x] [TASK-018](EPIC-012-backend-consolidation/TASK-018-remove-sync-tool-execute.md) Remove sync `Tool.Execute()` overloads — keep only `ExecuteAsync`
  - STORY-014 Retire DraCode.WebSocket and DraCode.Web — done (2/2)
    - [x] [TASK-029](EPIC-012-backend-consolidation/STORY-014-retire-old-stack/TASK-029-delete-old-stack.md) Retire DraCode.WebSocket and DraCode.Web
    - [x] [TASK-070](EPIC-012-backend-consolidation/STORY-014-retire-old-stack/TASK-070-scrub-full-project-spec.md) Retire FULL_PROJECT_SPECIFICATION.md (stale regeneration spec)
  - STORY-015 /kobold WebSocket endpoint — planned (0/5)
    - [ ] [TASK-037](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-037-run-event-source.md) Internal per-run event source (P1)
    - [ ] [TASK-038](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-038-kobold-endpoint-protocol.md) /kobold WebSocket endpoint + message protocol (P1)
    - [ ] [TASK-039](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-039-adhoc-mode.md) /kobold ad-hoc mode (P1)
    - [ ] [TASK-040](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-040-project-mode.md) /kobold project mode (P1)
    - [ ] [TASK-041](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-041-pathhelper-widening.md) Widen PathHelper for ad-hoc cwds (P2)
  - STORY-016 REST + SSE facade for non-streaming clients — planned (0/5)
    - [ ] [TASK-042](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-042-api-skeleton-openapi.md) /api/v1 minimal-API skeleton + OpenAPI (P1)
    - [ ] [TASK-043](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-043-resource-endpoints.md) Project / spec / feature / task / plan REST endpoints (P1)
    - [ ] [TASK-044](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-044-runs-endpoints.md) Runs endpoints (start + status) (P1)
    - [ ] [TASK-045](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-045-sse-events-endpoint.md) SSE stream: GET /api/v1/runs/{id}/events (P1)
    - [ ] [TASK-046](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-046-agents-cost-endpoints.md) agents/active + cost-report endpoints (P2)
  - STORY-017 OAuth/OIDC authentication and per-caller identity — in-progress (1/7)
    - [x] [TASK-030](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-030-host-oauth-server.md) Host Birko OAuth server endpoints in KoboldLair.Server
    - [ ] [TASK-031](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-031-sqlite-oauth-stores.md) SQLite-backed OAuth server stores (P1)
    - [ ] [TASK-032](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-032-jwt-validation-middleware.md) Enable JWT validation middleware (P1)
    - [ ] [TASK-033](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-033-github-federation.md) GitHub federation for human login (P1)
    - [ ] [TASK-034](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-034-user-entity-ownership-migration.md) User entity, project ownership, and projects.json migration (P1)
    - [ ] [TASK-035](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-035-service-account-registration.md) Service-account client registration (P2)
    - [ ] [TASK-036](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-036-deprecate-legacy-auth.md) Deprecate and remove legacy auth (P2)
  - STORY-018 Daemon mode for KoboldLair.Server — planned (0/3)
    - [ ] [TASK-047](EPIC-012-backend-consolidation/STORY-018-daemon-mode/TASK-047-daemon-lifecycle.md) Daemon lifecycle flags (P2)
    - [ ] [TASK-048](EPIC-012-backend-consolidation/STORY-018-daemon-mode/TASK-048-dynamic-port-state-file.md) Dynamic port + daemon.json state + absolute ProjectsPath (P2)
    - [ ] [TASK-049](EPIC-012-backend-consolidation/STORY-018-daemon-mode/TASK-049-discovery-helper-bypass.md) Client discovery helper + loopback auth-bypass (P2)
- **EPIC-013 Multi-platform clients on the unified backend** — planned (0/20)
  - STORY-019 DraCode.KoboldLair.Cli — single-file binary CLI client — planned (0/5)
    - [ ] [TASK-050](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-050-rename-scaffold-publish.md) Rename DraCode → DraCode.KoboldLair.Cli + single-file publish (P1)
    - [ ] [TASK-051](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-051-daemon-interaction.md) CLI daemon interaction + discovery (P1)
    - [ ] [TASK-052](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-052-run-verbs.md) CLI run verbs: do / run / merge (P1)
    - [ ] [TASK-053](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-053-interactive-verbs.md) CLI interactive verbs: chat / analyze + streaming UX (P1)
    - [ ] [TASK-054](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-054-auth-project-verbs.md) CLI auth + management verbs: login / keys / projects / status / stop (P1)
  - STORY-020 Discord bot client — planned (0/3)
    - [ ] [TASK-067](EPIC-013-multi-platform-clients/STORY-020-discord-bot/TASK-067-bot-scaffold-commands.md) Discord bot scaffold + config + slash-command registration (P2)
    - [ ] [TASK-068](EPIC-013-multi-platform-clients/STORY-020-discord-bot/TASK-068-commands-streaming.md) Discord commands + thread streaming (P2)
    - [ ] [TASK-069](EPIC-013-multi-platform-clients/STORY-020-discord-bot/TASK-069-login-linkage-session-store.md) Discord /login user linkage + thread→session store (P2)
  - STORY-021 VSCode extension client — planned (0/4)
    - [ ] [TASK-059](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-059-extension-scaffold.md) VSCode extension scaffold + manifest + packaging (P2)
    - [ ] [TASK-060](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-060-chat-runs-panels.md) VSCode Dragon chat panel + Runs panel (P2)
    - [ ] [TASK-061](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-061-auth-daemon-discovery.md) VSCode auth (vscode.authentication) + daemon discovery (P2)
    - [ ] [TASK-062](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-062-inline-diff-run-on-selection.md) VSCode inline diff view + Run on Selection (P2)
  - STORY-022 Python SDK — planned (0/4)
    - [ ] [TASK-063](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-063-package-scaffold-models.md) Python SDK scaffold + Pydantic models from OpenAPI (P2)
    - [ ] [TASK-064](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-064-sync-async-client.md) Python SDK sync + async client (P2)
    - [ ] [TASK-065](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-065-sse-streaming.md) Python SDK run streaming (SSE + polling fallback) (P2)
    - [ ] [TASK-066](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-066-pypi-publish-drift-guard.md) Python SDK PyPI publish + schema-drift guard (P2)
  - STORY-023 DraCode.KoboldLair.Client — UI polish reclaimed from EPIC-003 — planned (0/4)
    - [ ] [TASK-055](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-055-daemon-status-project-switcher.md) Web header: daemon status indicator + project switcher (P2)
    - [ ] [TASK-056](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-056-oauth-login-key-management.md) Web OAuth login button + service-account key management UI (P2)
    - [ ] [TASK-057](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-057-run-viewer.md) Web run viewer for external /kobold runs (P2)
    - [ ] [TASK-058](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-058-tab-reorder-workspace-layouts.md) Drag-and-drop tabs + workspace layout save/load (P2)
- **EPIC-014 Human-in-the-loop decision gates** — planned (0/6)
  - STORY-024 Blocking "AwaitingHumanDecision" escalation tier — planned (0/2)
    - [ ] [TASK-019](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/TASK-019-awaiting-human-decision-state.md) AwaitingHumanDecision state + HumanDecisionRequired disposition (P1)
    - [ ] [TASK-020](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/TASK-020-drake-parks-routes-to-human.md) Drake parks task & routes decision to human (P1)
  - STORY-025 Async `ask_human` tool for automatic agents — planned (0/2)
    - [ ] [TASK-021](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-021-async-ask-human-tool.md) Implement async `ask_human` tool (P1)
    - [ ] [TASK-022](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-022-deliver-answer-into-context.md) Deliver human answer back into agent context on resume (P2)
  - STORY-026 Policy-driven decision gates (per-project) — planned (0/1)
    - [ ] [TASK-023](EPIC-014-human-in-the-loop-gates/STORY-026-decision-gate-policy/TASK-023-decision-gate-policy-config.md) DecisionGatePolicy config + enforcement in escalation routing (P2)
  - STORY-027 Answerable decisions surfaced in Dragon — planned (0/1)
    - [ ] [TASK-024](EPIC-014-human-in-the-loop-gates/STORY-027-answerable-notifications/TASK-024-answerable-notifications-dragon.md) Answerable notifications + Dragon actionable prompt round-trip (P2)
- **EPIC-015 Self-reasoning depth & end-to-end verification** — planned (0/4)
  - STORY-028 End-to-end task acceptance verification — planned (0/1)
    - [ ] [TASK-025](EPIC-015-self-reasoning-depth/STORY-028-end-to-end-task-verification/TASK-025-task-acceptance-check.md) Post-plan task-acceptance check against acceptance criteria (P1)
  - STORY-029 Escalation loop closure — feed resolution back to the Kobold — planned (0/1)
    - [ ] [TASK-026](EPIC-015-self-reasoning-depth/STORY-029-escalation-loop-closure/TASK-026-feed-resolution-to-kobold.md) Feed escalation resolution back into Kobold context (P2)
  - STORY-030 Budget- and cost-aware reflection — planned (0/1)
    - [ ] [TASK-027](EPIC-015-self-reasoning-depth/STORY-030-budget-aware-reflection/TASK-027-budget-aware-reflection.md) Surface real token/cost spend into reflect tool & monitor (P2)
  - STORY-031 Cross-step coherence re-plan check — planned (0/1)
    - [ ] [TASK-028](EPIC-015-self-reasoning-depth/STORY-031-cross-step-replan-check/TASK-028-cross-step-coherence-check.md) Lightweight re-plan check when a step is revised/failed (P2)
- **EPIC-016 Database-backed agent working state** — planned (0/0)
  - STORY-032 Migrate remaining file-based working artifacts to the database — planned (0/0)
  - STORY-033 Persist structured decision & reasoning records — planned (0/0)
  - STORY-034 Codify and enforce the deliverable-vs-working-state boundary — planned (0/0)
- **EPIC-017 Generalized agent consensus** — planned (0/0)
  - STORY-035 Reusable ConsensusService abstraction — planned (0/0)
  - STORY-036 Diverse-panel consensus (heterogeneous agents) — planned (0/0)
  - STORY-037 Retrofit EPIC-010 voting integrations onto the shared mechanism — planned (0/0)

## Completed

<details>
<summary>1 completed epic</summary>

- **EPIC-006 Self-reasoning — Option 2 (structured tools)** — done
  - STORY-003 Structured reasoning tools — done (2/2)
    - [x] [TASK-011](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-011-reflection-tool.md) ReflectionTool — structured reasoning capture
    - [x] [TASK-012](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-012-reasoning-monitor-service.md) ReasoningMonitorService

</details>
