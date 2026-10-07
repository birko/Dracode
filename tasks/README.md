# Tasks — DraCode

_Generated 2026-10-07 (close TASK-093, TASK-084, TASK-092, TASK-105, TASK-104, TASK-085, TASK-083; new TASK-106). Run `/tasks triage` to refresh. **Do not hand-edit** — changes will be overwritten._

## Counts

| Status       | Epics | Stories | Tasks |
|--------------|-------|---------|-------|
| planned      | 12 | 30 | — |
| todo         | — | — | 54 |
| in-progress  | 2 | 4 | 0 |
| verify       | — | — | 0 |
| blocked      | — | — | 23 |
| done         | 1 | 2 | 47 |
| cancelled    | 0 | 0 | 0 |

_`blocked` is a flag, not a state: 23 of the 23 blocked tasks are also counted in their own state, so the rows do not sum to the task total._

`todo` by priority: 15× P1 · 38× P2 · 1× P3.

## In progress now

_None_

## In review (code complete, awaiting human sign-off)

_None_

## Tree

- **EPIC-001 Token storage & auth providers** — planned (0/2)
  - [ ] [TASK-001](EPIC-001-token-storage-auth/TASK-001-encrypted-token-storage.md) Encrypted token storage (P1) · FEATURE-001
  - [ ] [TASK-002](EPIC-001-token-storage-auth/TASK-002-oauth-integration.md) OAuth integration (Google, GitHub) (P2) ⚠ blocked · FEATURE-001
- **EPIC-002 Plugin & extensibility** — planned (0/1)
  - [ ] [TASK-003](EPIC-002-plugin-extensibility/TASK-003-plugin-system.md) Plugin system for custom tools (P2) · FEATURE-002
- **EPIC-004 Team & compliance** — planned (0/3)
  - STORY-001 Enterprise features — planned (0/3)
    - [ ] [TASK-006](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-006-team-collaboration.md) Team collaboration (RBAC + shared workspaces) (P1) · FEATURE-003
    - [ ] [TASK-007](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-007-audit-logging.md) Audit logging (P1) · FEATURE-003
    - [ ] [TASK-008](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-008-ci-cd-integration.md) CI/CD integration (GitHub Actions + Docker) (P1) · FEATURE-003
- **EPIC-005 Observability — Metrics + Health Dashboard** — planned (0/2)
  - STORY-002 Monitoring infrastructure — planned (0/2)
    - [ ] [TASK-009](EPIC-005-observability/STORY-002-monitoring-infra/TASK-009-prometheus-metrics.md) Prometheus /metrics endpoint (P2) · FEATURE-004
    - [ ] [TASK-010](EPIC-005-observability/STORY-002-monitoring-infra/TASK-010-health-dashboard.md) Health dashboard web UI (P2) ⚠ blocked · FEATURE-004
- **EPIC-008 Research / advanced ML** — planned (0/2)
  - [ ] [TASK-016](EPIC-008-research-advanced-ml/TASK-016-rag-codebase-understanding.md) RAG for codebase understanding (P2) · FEATURE-006
  - [ ] [TASK-017](EPIC-008-research-advanced-ml/TASK-017-fine-tuned-models.md) Fine-tuned models on project codebases (P2) · FEATURE-006
- **EPIC-009 Simplicity audit — collapse over-stacked agent tiers** — planned (0/0)
  - STORY-005 Evaluate merging Wyrm + Wyvern into one analyzer — planned (0/0)
  - STORY-006 Evaluate folding KoboldPlanner into Kobold — planned (0/0)
  - STORY-007 Evaluate collapsing DrakeExecutionService + DrakeMonitoringService + ReasoningMonitorService — planned (0/0)
- **EPIC-010 Add voting parallelization to high-stakes decisions** — planned (0/0)
  - STORY-008 Wyvern task breakdown voting — planned (0/0)
  - STORY-009 WyrmAgent agent-type selection voting — planned (0/0)
  - STORY-010 KoboldPlanner plan voting — planned (0/0)
- **EPIC-011 Tight evaluator-optimizer loop for Kobold actions** — planned (0/0)
  - STORY-011 Design EvaluatorAgent — planned (0/0)
  - STORY-012 Integrate EvaluatorAgent into the Kobold tool loop — planned (0/0)
  - STORY-013 Measure: does external evaluator catch failures the reflect tool misses? — planned (0/0)
- **EPIC-012 Backend consolidation — unify on KoboldLair.Server** — in-progress (19/28)
  - [x] [TASK-071](EPIC-012-backend-consolidation/TASK-071-unify-framework-source-compilation.md) Unify Birko framework source compilation into DraCode.Birko (P1) · FEATURE-077
  - [x] [TASK-018](EPIC-012-backend-consolidation/TASK-018-remove-sync-tool-execute.md) Remove sync `Tool.Execute()` overloads — keep only `ExecuteAsync` (P2) · FEATURE-021
  - STORY-014 Retire DraCode.WebSocket and DraCode.Web — done (2/2) (done)
    - [x] [TASK-029](EPIC-012-backend-consolidation/STORY-014-retire-old-stack/TASK-029-delete-old-stack.md) Retire DraCode.WebSocket and DraCode.Web (P1) · FEATURE-016
    - [x] [TASK-070](EPIC-012-backend-consolidation/STORY-014-retire-old-stack/TASK-070-scrub-full-project-spec.md) Retire FULL_PROJECT_SPECIFICATION.md (stale regeneration spec) (P2) · FEATURE-016
  - STORY-015 /kobold WebSocket endpoint — ad-hoc + project-scoped Kobold execution — in-progress (6/7)
    - [x] [TASK-037](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-037-run-event-source.md) Internal per-run event source (Kobold tool-loop event sink) (P1) · FEATURE-017
    - [x] [TASK-038](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-038-kobold-endpoint-protocol.md) /kobold WebSocket endpoint + message protocol (P1)
    - [x] [TASK-039](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-039-adhoc-mode.md) /kobold ad-hoc mode (P1) · FEATURE-017
    - [x] [TASK-040](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-040-project-mode.md) /kobold project mode (P1)
    - [x] [TASK-095](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-095-per-project-drake-switch.md) Per-project Drake switch: Drake skips a project whose Drake is off, /kobold project mode still runs it (P1) · FEATURE-017
    - [x] [TASK-100](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-100-project-mode-drake-never-released.md) /kobold project mode never releases its Drake, so the project's background Drake is locked out afterwards (P1) · FEATURE-017
    - [ ] [TASK-041](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-041-pathhelper-widening.md) Widen PathHelper for ad-hoc cwds (P2) · FEATURE-017
  - STORY-016 REST + SSE facade for non-streaming clients — in-progress (3/7)
    - [x] [TASK-042](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-042-api-skeleton-openapi.md) /api/v1 minimal-API skeleton + OpenAPI (P1) · FEATURE-018
    - [x] [TASK-043](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-043-resource-endpoints.md) Project / spec / feature / task / plan REST endpoints (P1) · FEATURE-018
    - [x] [TASK-044](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-044-runs-endpoints.md) Runs endpoints (start + status) (P1) · FEATURE-018
    - [ ] [TASK-045](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-045-sse-events-endpoint.md) SSE stream: GET /api/v1/runs/{id}/events (P1) · FEATURE-018
    - [ ] [TASK-046](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-046-agents-cost-endpoints.md) agents/active + cost-report endpoints (P2) · FEATURE-018
    - [ ] [TASK-074](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-074-rest-delete-project-policy.md) REST DELETE /projects — match the Dragon delete policy + clean up files (P2) · FEATURE-018
    - [ ] [TASK-075](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-075-serialize-rest-spec-feature-edits.md) Serialize concurrent REST spec/feature edits (lost-update guard) (P2) · FEATURE-018
  - STORY-017 OAuth/OIDC authentication and per-caller identity — in-progress (6/7)
    - [x] [TASK-030](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-030-host-oauth-server.md) Host Birko OAuth server endpoints in KoboldLair.Server (P1) · FEATURE-019
    - [x] [TASK-031](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-031-sqlite-oauth-stores.md) SQLite-backed OAuth server stores (P1) · FEATURE-019
    - [x] [TASK-032](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-032-jwt-validation-middleware.md) Enable JWT validation middleware (P1) · FEATURE-019
    - [x] [TASK-033](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-033-github-federation.md) GitHub federation for human login (P1) · FEATURE-019
    - [x] [TASK-034](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-034-user-entity-ownership-migration.md) User entity, project ownership, and projects.json migration (P1) · FEATURE-019
    - [x] [TASK-035](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-035-service-account-registration.md) Service-account client registration (P2) · FEATURE-019
    - [ ] [TASK-036](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-036-deprecate-legacy-auth.md) Deprecate and remove legacy auth (P2) · FEATURE-019
  - STORY-018 Daemon mode for KoboldLair.Server — planned (0/3)
    - [ ] [TASK-047](EPIC-012-backend-consolidation/STORY-018-daemon-mode/TASK-047-daemon-lifecycle.md) Daemon lifecycle flags (P2) · FEATURE-020
    - [ ] [TASK-048](EPIC-012-backend-consolidation/STORY-018-daemon-mode/TASK-048-dynamic-port-state-file.md) Dynamic port + daemon.json state + absolute ProjectsPath (P2) ⚠ blocked · FEATURE-020
    - [ ] [TASK-049](EPIC-012-backend-consolidation/STORY-018-daemon-mode/TASK-049-discovery-helper-bypass.md) Client discovery helper + loopback auth-bypass (P2) ⚠ blocked · FEATURE-020
- **EPIC-013 Multi-platform clients on the unified backend** — planned (0/21)
  - STORY-019 DraCode.KoboldLair.Cli — single-file binary CLI client — planned (0/5)
    - [ ] [TASK-050](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-050-rename-scaffold-publish.md) Rename DraCode → DraCode.KoboldLair.Cli + single-file publish (P1) · FEATURE-022
    - [ ] [TASK-051](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-051-daemon-interaction.md) CLI daemon interaction + discovery (P1) ⚠ blocked · FEATURE-022
    - [ ] [TASK-052](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-052-run-verbs.md) CLI run verbs: do / run / merge (P1) ⚠ blocked · FEATURE-022
    - [ ] [TASK-053](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-053-interactive-verbs.md) CLI interactive verbs: chat / analyze + streaming UX (P1) ⚠ blocked · FEATURE-022
    - [ ] [TASK-054](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-054-auth-project-verbs.md) CLI auth + management verbs: login / keys / projects / status / stop (P1) ⚠ blocked · FEATURE-022
  - STORY-020 Discord bot client — planned (0/3)
    - [ ] [TASK-067](EPIC-013-multi-platform-clients/STORY-020-discord-bot/TASK-067-bot-scaffold-commands.md) Discord bot scaffold + config + slash-command registration (P2) · FEATURE-023
    - [ ] [TASK-068](EPIC-013-multi-platform-clients/STORY-020-discord-bot/TASK-068-commands-streaming.md) Discord commands + thread streaming (P2) ⚠ blocked · FEATURE-023
    - [ ] [TASK-069](EPIC-013-multi-platform-clients/STORY-020-discord-bot/TASK-069-login-linkage-session-store.md) Discord /login user linkage + thread→session store (P2) ⚠ blocked · FEATURE-023
  - STORY-021 VSCode extension client — planned (0/4)
    - [ ] [TASK-059](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-059-extension-scaffold.md) VSCode extension scaffold + manifest + packaging (P2) · FEATURE-024
    - [ ] [TASK-060](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-060-chat-runs-panels.md) VSCode Dragon chat panel + Runs panel (P2) ⚠ blocked · FEATURE-024
    - [ ] [TASK-061](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-061-auth-daemon-discovery.md) VSCode auth (vscode.authentication) + daemon discovery (P2) ⚠ blocked · FEATURE-024
    - [ ] [TASK-062](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-062-inline-diff-run-on-selection.md) VSCode inline diff view + Run on Selection (P2) ⚠ blocked · FEATURE-024
  - STORY-022 Python SDK — planned (0/4)
    - [ ] [TASK-063](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-063-package-scaffold-models.md) Python SDK scaffold + Pydantic models from OpenAPI (P2) · FEATURE-025
    - [ ] [TASK-064](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-064-sync-async-client.md) Python SDK sync + async client (P2) ⚠ blocked · FEATURE-025
    - [ ] [TASK-065](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-065-sse-streaming.md) Python SDK run streaming (SSE + polling fallback) (P2) ⚠ blocked · FEATURE-025
    - [ ] [TASK-066](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-066-pypi-publish-drift-guard.md) Python SDK PyPI publish + schema-drift guard (P2) ⚠ blocked · FEATURE-025
  - STORY-023 DraCode.KoboldLair.Client — UI polish reclaimed from EPIC-003 — planned (0/5)
    - [ ] [TASK-072](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-072-mount-ts-shell-in-served-page.md) Mount the TypeScript shell in the served page (P1) · FEATURE-026
    - [ ] [TASK-055](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-055-daemon-status-project-switcher.md) Web header: daemon status indicator + project switcher (P2) · FEATURE-026
    - [ ] [TASK-056](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-056-oauth-login-key-management.md) Web OAuth login button + service-account key management UI (P2) ⚠ blocked · FEATURE-026
    - [ ] [TASK-057](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-057-run-viewer.md) Web run viewer for external /kobold runs (P2) ⚠ blocked · FEATURE-026
    - [ ] [TASK-058](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-058-tab-reorder-workspace-layouts.md) Drag-and-drop tabs + workspace layout save/load (P2) · FEATURE-026
- **EPIC-014 Human-in-the-loop decision gates** — planned (0/6)
  - STORY-024 Blocking "AwaitingHumanDecision" escalation tier — planned (0/2)
    - [ ] [TASK-019](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/TASK-019-awaiting-human-decision-state.md) AwaitingHumanDecision state + HumanDecisionRequired disposition (P1) · FEATURE-027
    - [ ] [TASK-020](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/TASK-020-drake-parks-routes-to-human.md) Drake parks task & routes decision to human instead of auto-resolving (P1) ⚠ blocked · FEATURE-027
  - STORY-025 Async `ask_human` tool for automatic agents — planned (0/2)
    - [ ] [TASK-021](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-021-async-ask-human-tool.md) Implement async `ask_human` tool (P1) ⚠ blocked · FEATURE-028
    - [ ] [TASK-022](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-022-deliver-answer-into-context.md) Deliver human answer back into agent context on resume (P2) ⚠ blocked · FEATURE-028
  - STORY-026 Policy-driven decision gates (per-project) — planned (0/1)
    - [ ] [TASK-023](EPIC-014-human-in-the-loop-gates/STORY-026-decision-gate-policy/TASK-023-decision-gate-policy-config.md) DecisionGatePolicy config + enforcement in escalation routing (P2) ⚠ blocked · FEATURE-029
  - STORY-027 Answerable decisions surfaced in Dragon — planned (0/1)
    - [ ] [TASK-024](EPIC-014-human-in-the-loop-gates/STORY-027-answerable-notifications/TASK-024-answerable-notifications-dragon.md) Answerable notifications + Dragon actionable prompt round-trip (P2) ⚠ blocked · FEATURE-030
- **EPIC-015 Self-reasoning depth & end-to-end verification** — planned (0/4)
  - STORY-028 End-to-end task acceptance verification — planned (0/1)
    - [ ] [TASK-025](EPIC-015-self-reasoning-depth/STORY-028-end-to-end-task-verification/TASK-025-task-acceptance-check.md) Post-plan task-acceptance check against acceptance criteria (P1) · FEATURE-031
  - STORY-029 Escalation loop closure — feed resolution back to the Kobold — planned (0/1)
    - [ ] [TASK-026](EPIC-015-self-reasoning-depth/STORY-029-escalation-loop-closure/TASK-026-feed-resolution-to-kobold.md) Feed escalation resolution back into Kobold context (P2) · FEATURE-032
  - STORY-030 Budget- and cost-aware reflection — planned (0/1)
    - [ ] [TASK-027](EPIC-015-self-reasoning-depth/STORY-030-budget-aware-reflection/TASK-027-budget-aware-reflection.md) Surface real token/cost spend into reflect tool & monitor (P2) · FEATURE-033
  - STORY-031 Cross-step coherence re-plan check — planned (0/1)
    - [ ] [TASK-028](EPIC-015-self-reasoning-depth/STORY-031-cross-step-replan-check/TASK-028-cross-step-coherence-check.md) Lightweight re-plan check when a step is revised/failed (P2) · FEATURE-034
- **EPIC-016 Database-backed agent working state** — in-progress (2/2)
  - STORY-032 Migrate remaining file-based working artifacts to the database — in-progress (2/2)
    - [x] [TASK-094](EPIC-016-db-backed-agent-state/STORY-032-migrate-remaining-artifacts/TASK-094-remove-project-configs-json.md) Remove project-configs.json — the database is the only store for per-project agent settings (P1)
    - [x] [TASK-096](EPIC-016-db-backed-agent-state/STORY-032-migrate-remaining-artifacts/TASK-096-remove-dead-project-config-client.md) Remove the dead project-config-client.js (calls /api/project-configs routes nothing serves) (P2)
  - STORY-033 Persist structured decision & reasoning records — planned (0/0)
  - STORY-034 Codify and enforce the deliverable-vs-working-state boundary — planned (0/0)
- **EPIC-017 Generalized agent consensus** — planned (0/0)
  - STORY-035 Reusable ConsensusService abstraction — planned (0/0)
  - STORY-036 Diverse-panel consensus (heterogeneous agents) — planned (0/0)
  - STORY-037 Retrofit EPIC-010 voting integrations onto the shared mechanism — planned (0/0)

## Loose tasks

- [x] [TASK-073](_loose/TASK-073-db-backed-provider-config.md) Store LLM provider configuration in the database (editable, not files/env) (P1)
- [x] [TASK-078](_loose/TASK-078-migrate-the-41-tool-overrides-to-the-token-signature.md) Migrate the 41 tool overrides — this repo has not compiled since 2026-07-09 (P1)
- [x] [TASK-082](_loose/TASK-082-usage-records-missing-estimated-cost-column.md) Usage records are never saved: the usage_records table has no EstimatedCostUsd column (P1)
- [x] [TASK-088](_loose/TASK-088-provider-registry-empty-for-dragon-on-fresh-server.md) Dragon fails on a fresh server: "Provider 'zai' is not registered" (empty provider registry) (P1)
- [x] [TASK-089](_loose/TASK-089-project-provider-override-name-vs-type.md) Wyvern/Wyrm project provider overrides pass a provider name where a type is needed (P1)
- [x] [TASK-090](_loose/TASK-090-new-projects-start-with-agents-disabled.md) A project created while the server runs is never analyzed: its agents start disabled (P1)
- [x] [TASK-076](_loose/TASK-076-birko-owned-package-versions-below-the-framework.md) Birko-owned package versions were below the framework, and one pin was holding a High advisory open (P2)
- [x] [TASK-077](_loose/TASK-077-clear-the-two-high-advisories-deferred-by-task-076.md) Clear the two High advisories TASK-076 deferred (P2)
- [ ] [TASK-079](_loose/TASK-079-thread-the-cancellation-token-through-tool-bodies.md) Thread the cancellation token through the tool bodies (P2)
- [ ] [TASK-080](_loose/TASK-080-rename-domainevententity-aggregateid.md) Rename DomainEventEntity.AggregateId to follow Birko's Guid naming rule (P2)
- [x] [TASK-084](_loose/TASK-084-agent-provider-setting-names-missing-provider.md) A stored agent provider setting that names a missing provider breaks Dragon completely (P2)
- [x] [TASK-086](_loose/TASK-086-zai-coding-plan-for-non-coding-agents.md) Z.AI: Dragon, Wyrm and Wyvern fail with "insufficient balance" and the model list is out of date (P2)
- [x] [TASK-091](_loose/TASK-091-new-project-tasks-not-in-database.md) Tasks of a new project never reach the database, so the REST task endpoints can't see them (P2)
- [x] [TASK-092](_loose/TASK-092-view-cost-report-tool-not-wired.md) The view_cost_report tool exists but no agent has it (P2)
- [x] [TASK-093](_loose/TASK-093-deleted-project-wyvern-reused-by-name.md) A deleted project's Wyvern stays registered by name and is reused by a new project with that name (P2)
- [x] [TASK-097](_loose/TASK-097-spec-path-lookup-fails-on-mixed-separators.md) Approved features never reach Wyvern when ProjectsPath uses forward slashes (spec-path lookup compares mixed separators) (P1)
- [x] [TASK-098](_loose/TASK-098-empty-wyvern-reply-becomes-zero-task-analysis.md) An empty or unparseable Wyvern reply silently becomes a 0-task analysis and the project is marked Analyzed (P1)
- [x] [TASK-099](_loose/TASK-099-features-never-passed-to-wyvern.md) Approved features are never given to Wyvern — analysis sees only specification.md and features stay Ready (P2)
- [x] [TASK-101](_loose/TASK-101-diagnose-next-unusable-wyvern-reply.md) Find the root cause of empty Wyvern replies once the new log captures one (P3)
- [x] [TASK-102](_loose/TASK-102-tasks-never-linked-to-features.md) Wyvern tasks are never linked to their features, so no work lands on feature branches; re-analysis of a new feature yields no task for it (P2)
- [x] [TASK-103](_loose/TASK-103-delete-project-leaves-git-repo-and-task-rows.md) delete_project reports success but leaves a git project's folder and the project's task rows behind (P2)
- [x] [TASK-104](_loose/TASK-104-test-host-loads-developer-appsettings-local.md) The API test host loads the developer's appsettings.local.json, so tests depend on the machine they run on (P2)
- [x] [TASK-105](_loose/TASK-105-only-kobold-llm-calls-are-cost-tracked.md) Only Kobold LLM calls are cost-tracked — Dragon, the council, Wyrm, Wyvern and the planner are never recorded (P2)
- [ ] [TASK-106](_loose/TASK-106-planless-runs-emit-no-tool-call-events.md) A run without the enhanced plan path publishes no tool-call events — `/kobold` ad-hoc clients see no progress (P2)
- [ ] [TASK-081](_loose/TASK-081-decide-koboldlair-sqlite-reference.md) Decide whether DraCode.KoboldLair needs its own Microsoft.Data.Sqlite reference (P3)
- [x] [TASK-083](_loose/TASK-083-run-status-stays-pending-without-plan.md) A run without a plan reports "pending" until it finishes — it never shows "running" (P3)
- [x] [TASK-085](_loose/TASK-085-dragon-sends-specification-created-after-failed-request.md) Dragon sends specification_created after a request that failed (P3)
- [x] [TASK-087](_loose/TASK-087-use-birko-ensurecolumns-in-usage-repository.md) Replace SqlUsageRepository's local add-missing-columns helper with Birko's EnsureColumns (P3)

<details><summary>Completed</summary>

- **EPIC-006 Self-reasoning — Option 2 (structured tools)** — done (2/2)
  - STORY-003 Structured reasoning tools — done (2/2) (done)
    - [x] [TASK-011](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-011-reflection-tool.md) ReflectionTool — structured reasoning capture (P2) · FEATURE-005
    - [x] [TASK-012](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-012-reasoning-monitor-service.md) ReasoningMonitorService (P2) · FEATURE-005

</details>
