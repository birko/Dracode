# Tasks — DraCode

_Generated 2026-11-25 12:24. Run `/tasks triage` to refresh. **Do not hand-edit** — changes will be overwritten._

## Counts

| Status       | Epics              | Stories            | Tasks               |
|--------------|--------------------|--------------------|---------------------|
| planned      | 10                 | 34                 | —                   |
| todo         | —                  | —                  | 86                  |
| in-progress  | 7                  | 12                 | 0                   |
| verify       | —                  | —                  | 0                   |
| blocked      | —                  | —                  | 20 (20 also counted in their own state) |
| done         | 1                  | 7                  | 128                 |
| cancelled    | 0                  | 0                  | 5                   |

`todo` by priority: 9× P1 · 52× P2 · 25× P3.

## In progress now

_None_

## Tree

> Parents out of step (8): STORY-025 in-progress, every child closed → `/tasks close STORY-025`, or keep it open; STORY-027 in-progress, every child closed → `/tasks close STORY-027`, or keep it open; STORY-032 in-progress, every child closed → `/tasks close STORY-032`, or keep it open; STORY-042 in-progress, every child closed → `/tasks close STORY-042`, or keep it open; STORY-043 planned, every child closed → `/tasks close STORY-043`, or keep it open; STORY-046 planned, every child closed → `/tasks close STORY-046`, or keep it open; STORY-047 planned, every child closed → `/tasks close STORY-047`, or keep it open; STORY-048 planned, every child closed → `/tasks close STORY-048`, or keep it open.

- **EPIC-001 Token storage & auth providers** — planned (0/2 tasks done)
  - (epic level) — 0/2 done
    - [ ] [TASK-001](EPIC-001-token-storage-auth/TASK-001-encrypted-token-storage.md) Encrypted token storage · FEATURE-001
    - [ ] [TASK-002](EPIC-001-token-storage-auth/TASK-002-oauth-integration.md) OAuth integration (Google, GitHub) ⏳ waits on TASK-001 ⚠ blocked: waiting on TASK-001 · FEATURE-001
- **EPIC-002 Plugin & extensibility** — planned (0/1 tasks done)
  - (epic level) — 0/1 done
    - [ ] [TASK-003](EPIC-002-plugin-extensibility/TASK-003-plugin-system.md) Plugin system for custom tools · FEATURE-002
- **EPIC-004 Team & compliance** — planned (0/3 tasks done)
  - STORY-001 Enterprise features — planned (0/3 done)
    - [ ] [TASK-006](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-006-team-collaboration.md) Team collaboration (RBAC + shared workspaces) · FEATURE-003
    - [ ] [TASK-007](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-007-audit-logging.md) Audit logging · FEATURE-003
    - [ ] [TASK-008](EPIC-004-team-compliance/STORY-001-enterprise-features/TASK-008-ci-cd-integration.md) CI/CD integration (GitHub Actions + Docker) · FEATURE-003
- **EPIC-005 Observability — Metrics + Health Dashboard** — planned (0/2 tasks done)
  - STORY-002 Monitoring infrastructure — planned (0/2 done)
    - [ ] [TASK-009](EPIC-005-observability/STORY-002-monitoring-infra/TASK-009-prometheus-metrics.md) Prometheus /metrics endpoint · FEATURE-004
    - [ ] [TASK-010](EPIC-005-observability/STORY-002-monitoring-infra/TASK-010-health-dashboard.md) Health dashboard web UI ⏳ waits on TASK-009 ⚠ blocked: waiting on TASK-009 · FEATURE-004
- **EPIC-008 Research / advanced ML** — planned (0/2 tasks done)
  - (epic level) — 0/2 done
    - [ ] [TASK-016](EPIC-008-research-advanced-ml/TASK-016-rag-codebase-understanding.md) RAG for codebase understanding · FEATURE-006
    - [ ] [TASK-017](EPIC-008-research-advanced-ml/TASK-017-fine-tuned-models.md) Fine-tuned models on project codebases · FEATURE-006
- **EPIC-009 Simplicity audit — collapse over-stacked agent tiers** — planned (0/0 tasks done)
  - STORY-005 Evaluate merging Wyrm + Wyvern into one analyzer — planned (0/0 done)
  - STORY-006 Evaluate folding KoboldPlanner into Kobold — planned (0/0 done)
  - STORY-007 Evaluate collapsing DrakeExecutionService + DrakeMonitoringService + ReasoningMonitorService — planned (0/0 done)
- **EPIC-010 Add voting parallelization to high-stakes decisions** — planned (0/0 tasks done)
  - STORY-008 Wyvern task breakdown voting — planned (0/0 done)
  - STORY-009 WyrmAgent agent-type selection voting — planned (0/0 done)
  - STORY-010 KoboldPlanner plan voting — planned (0/0 done)
- **EPIC-011 Tight evaluator-optimizer loop for Kobold actions** — planned (0/0 tasks done)
  - STORY-011 Design EvaluatorAgent — planned (0/0 done)
  - STORY-012 Integrate EvaluatorAgent into the Kobold tool loop — planned (0/0 done)
  - STORY-013 Measure: does external evaluator catch failures the reflect tool misses? — planned (0/0 done)
- **EPIC-012 Backend consolidation — unify on KoboldLair.Server** — in-progress (21/30 tasks done)
  - STORY-014 Retire DraCode.WebSocket and DraCode.Web — done (2/2 done)
    - [x] [TASK-029](EPIC-012-backend-consolidation/STORY-014-retire-old-stack/TASK-029-delete-old-stack.md) Retire DraCode.WebSocket and DraCode.Web · FEATURE-016
    - [x] [TASK-070](EPIC-012-backend-consolidation/STORY-014-retire-old-stack/TASK-070-scrub-full-project-spec.md) Retire FULL_PROJECT_SPECIFICATION.md (stale regeneration spec) · FEATURE-016
  - STORY-015 /kobold WebSocket endpoint — ad-hoc + project-scoped Kobold execution — in-progress (6/7 done)
    - [x] [TASK-037](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-037-run-event-source.md) Internal per-run event source (Kobold tool-loop event sink) · FEATURE-017
    - [x] [TASK-038](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-038-kobold-endpoint-protocol.md) /kobold WebSocket endpoint + message protocol · FEATURE-017
    - [x] [TASK-039](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-039-adhoc-mode.md) /kobold ad-hoc mode · FEATURE-017
    - [x] [TASK-040](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-040-project-mode.md) /kobold project mode · FEATURE-017
    - [x] [TASK-095](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-095-per-project-drake-switch.md) Per-project Drake switch: Drake skips a project whose Drake is off, /kobold project mode still runs it · FEATURE-017
    - [x] [TASK-100](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-100-project-mode-drake-never-released.md) /kobold project mode never releases its Drake, so the project's background Drake is locked out afterwards · FEATURE-017
    - [ ] [TASK-041](EPIC-012-backend-consolidation/STORY-015-kobold-endpoint/TASK-041-pathhelper-widening.md) Widen PathHelper for ad-hoc cwds · FEATURE-017
  - STORY-016 REST + SSE facade for non-streaming clients — in-progress (4/9 done)
    - [x] [TASK-042](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-042-api-skeleton-openapi.md) /api/v1 minimal-API skeleton + OpenAPI · FEATURE-018
    - [x] [TASK-043](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-043-resource-endpoints.md) Project / spec / feature / task / plan REST endpoints · FEATURE-018
    - [x] [TASK-044](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-044-runs-endpoints.md) Runs endpoints (start + status) · FEATURE-018
    - [x] [TASK-045](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-045-sse-events-endpoint.md) SSE stream: GET /api/v1/runs/{id}/events · FEATURE-018
    - [ ] [TASK-046](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-046-agents-cost-endpoints.md) agents/active + cost-report endpoints · FEATURE-018
    - [ ] [TASK-074](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-074-rest-delete-project-policy.md) REST DELETE /projects — match the Dragon delete policy + clean up files · FEATURE-018
    - [ ] [TASK-075](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-075-serialize-rest-spec-feature-edits.md) Serialize concurrent REST spec/feature edits (lost-update guard) · FEATURE-018
    - [ ] [TASK-107](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-107-verify-sse-behind-nginx.md) Verify the run events stream survives behind nginx · FEATURE-018
    - [ ] [TASK-185](EPIC-012-backend-consolidation/STORY-016-rest-sse-facade/TASK-185-persist-runs.md) Runs survive a server restart · FEATURE-026
  - STORY-017 OAuth/OIDC authentication and per-caller identity — done (7/7 done)
    - [x] [TASK-030](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-030-host-oauth-server.md) Host Birko OAuth server endpoints in KoboldLair.Server · FEATURE-019
    - [x] [TASK-031](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-031-sqlite-oauth-stores.md) SQLite-backed OAuth server stores · FEATURE-019
    - [x] [TASK-032](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-032-jwt-validation-middleware.md) Enable JWT validation middleware · FEATURE-019
    - [x] [TASK-033](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-033-github-federation.md) GitHub federation for human login · FEATURE-019
    - [x] [TASK-034](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-034-user-entity-ownership-migration.md) User entity, project ownership, and projects.json migration · FEATURE-019
    - [x] [TASK-035](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-035-service-account-registration.md) Service-account client registration · FEATURE-019
    - [x] [TASK-036](EPIC-012-backend-consolidation/STORY-017-oauth-identity/TASK-036-deprecate-legacy-auth.md) Deprecate and remove legacy auth · FEATURE-019
  - STORY-018 Daemon mode for KoboldLair.Server — planned (0/3 done)
    - [ ] [TASK-047](EPIC-012-backend-consolidation/STORY-018-daemon-mode/TASK-047-daemon-lifecycle.md) Daemon lifecycle flags · FEATURE-020
    - [ ] [TASK-048](EPIC-012-backend-consolidation/STORY-018-daemon-mode/TASK-048-dynamic-port-state-file.md) Dynamic port + daemon.json state + absolute ProjectsPath ⏳ waits on TASK-047 ⚠ blocked: waiting on TASK-047 · FEATURE-020
    - [ ] [TASK-049](EPIC-012-backend-consolidation/STORY-018-daemon-mode/TASK-049-discovery-helper-bypass.md) Client discovery helper + loopback auth-bypass ⏳ waits on TASK-048 ⚠ blocked: waiting on TASK-048 · FEATURE-020
  - (epic level) — 2/2 done
    - [x] [TASK-071](EPIC-012-backend-consolidation/TASK-071-unify-framework-source-compilation.md) Unify Birko framework source compilation into DraCode.Birko · FEATURE-077
    - [x] [TASK-018](EPIC-012-backend-consolidation/TASK-018-remove-sync-tool-execute.md) Remove sync `Tool.Execute()` overloads — keep only `ExecuteAsync` · FEATURE-021
- **EPIC-013 Multi-platform clients on the unified backend** — in-progress (31/49 tasks done)
  - STORY-019 DraCode.KoboldLair.Cli — single-file binary CLI client — planned (0/5 done)
    - [ ] [TASK-050](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-050-rename-scaffold-publish.md) Rename DraCode → DraCode.KoboldLair.Cli + single-file publish · FEATURE-022
    - [ ] [TASK-051](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-051-daemon-interaction.md) CLI daemon interaction + discovery ⏳ waits on TASK-050, TASK-049 ⚠ blocked: waiting on TASK-050, TASK-049 · FEATURE-022
    - [ ] [TASK-052](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-052-run-verbs.md) CLI run verbs: do / run / merge ⏳ waits on TASK-051 ⚠ blocked: waiting on TASK-051 · FEATURE-022
    - [ ] [TASK-053](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-053-interactive-verbs.md) CLI interactive verbs: chat / analyze + streaming UX ⏳ waits on TASK-051 ⚠ blocked: waiting on TASK-051 · FEATURE-022
    - [ ] [TASK-054](EPIC-013-multi-platform-clients/STORY-019-cli-client/TASK-054-auth-project-verbs.md) CLI auth + management verbs: login / keys / projects / status / stop ⏳ waits on TASK-051 ⚠ blocked: waiting on TASK-051 · FEATURE-022
  - STORY-020 Discord bot client — planned (0/3 done)
    - [ ] [TASK-067](EPIC-013-multi-platform-clients/STORY-020-discord-bot/TASK-067-bot-scaffold-commands.md) Discord bot scaffold + config + slash-command registration · FEATURE-023
    - [ ] [TASK-068](EPIC-013-multi-platform-clients/STORY-020-discord-bot/TASK-068-commands-streaming.md) Discord commands + thread streaming ⏳ waits on TASK-067 ⚠ blocked: waiting on TASK-067 · FEATURE-023
    - [ ] [TASK-069](EPIC-013-multi-platform-clients/STORY-020-discord-bot/TASK-069-login-linkage-session-store.md) Discord /login user linkage + thread→session store ⏳ waits on TASK-067 ⚠ blocked: waiting on TASK-067 · FEATURE-023
  - STORY-021 VSCode extension client — planned (0/4 done)
    - [ ] [TASK-059](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-059-extension-scaffold.md) VSCode extension scaffold + manifest + packaging · FEATURE-024
    - [ ] [TASK-060](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-060-chat-runs-panels.md) VSCode Dragon chat panel + Runs panel ⏳ waits on TASK-059 ⚠ blocked: waiting on TASK-059 · FEATURE-024
    - [ ] [TASK-061](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-061-auth-daemon-discovery.md) VSCode auth (vscode.authentication) + daemon discovery ⏳ waits on TASK-059 ⚠ blocked: waiting on TASK-059 · FEATURE-024
    - [ ] [TASK-062](EPIC-013-multi-platform-clients/STORY-021-vscode-extension/TASK-062-inline-diff-run-on-selection.md) VSCode inline diff view + Run on Selection ⏳ waits on TASK-060 ⚠ blocked: waiting on TASK-060 · FEATURE-024
  - STORY-022 Python SDK — planned (0/4 done)
    - [ ] [TASK-063](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-063-package-scaffold-models.md) Python SDK scaffold + Pydantic models from OpenAPI · FEATURE-025
    - [ ] [TASK-064](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-064-sync-async-client.md) Python SDK sync + async client ⏳ waits on TASK-063 ⚠ blocked: waiting on TASK-063 · FEATURE-025
    - [ ] [TASK-065](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-065-sse-streaming.md) Python SDK run streaming (SSE + polling fallback) ⏳ waits on TASK-064 ⚠ blocked: waiting on TASK-064 · FEATURE-025
    - [ ] [TASK-066](EPIC-013-multi-platform-clients/STORY-022-python-sdk/TASK-066-pypi-publish-drift-guard.md) Python SDK PyPI publish + schema-drift guard ⏳ waits on TASK-063 ⚠ blocked: waiting on TASK-063 · FEATURE-025
  - STORY-023 Web UI: app frame, sign-in and project picker — in-progress (8/10 done)
    - [x] [TASK-056](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-056-github-sign-in.md) Sign in with GitHub in the web UI · FEATURE-026
    - [x] [TASK-072](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-072-mount-ts-shell-in-served-page.md) Mount the TypeScript shell in the served page · FEATURE-026
    - ~~[TASK-142](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-142-wire-views-to-the-server.md) Wire the web client's views to the server; they show mock data~~ · FEATURE-026
    - [x] [TASK-145](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-145-app-frame-projects-homepage.md) App frame and projects homepage · FEATURE-026
    - ~~[TASK-055](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-055-daemon-status-project-switcher.md) Web header: daemon status indicator + project switcher~~ · FEATURE-026
    - [x] [TASK-058](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-058-tab-reorder-workspace-layouts.md) Drag-and-drop tabs + workspace layout save/load · FEATURE-026
    - [x] [TASK-146](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-146-header-project-picker.md) Header project picker inside a project · FEATURE-026
    - [x] [TASK-197](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-197-divider-drag-leaves-gap-beside-dragon.md) Dragging the workspace divider leaves a gap beside Dragon, and the dragged width is lost · FEATURE-026
    - [ ] [TASK-143](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-143-public-preview-deployments.md) Public preview deployments of projects on subdomains · FEATURE-026
    - [ ] [TASK-144](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-144-phone-push-notifications.md) Phone push notifications when a project needs you · FEATURE-026
    - [x] [TASK-198](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-198-use-split-panel-column-minimums.md) The workspace divider stops at the tabs' and Dragon's minimums while it is dragged · FEATURE-026
    - [x] [TASK-199](EPIC-013-multi-platform-clients/STORY-023-web-client-polish/TASK-199-phone-tab-focus-ring-inside-the-tab.md) The phone tab bar's focus ring sits inside the tab again · FEATURE-026
  - STORY-045 Web UI: projects homepage overview — done (3/3 done)
    - [x] [TASK-147](EPIC-013-multi-platform-clients/STORY-045-projects-homepage/TASK-147-needs-you.md) What needs you: homepage list and header pill · FEATURE-026
    - [x] [TASK-148](EPIC-013-multi-platform-clients/STORY-045-projects-homepage/TASK-148-cross-project-charts.md) Cross-project charts and numbers on the homepage · FEATURE-026
    - [x] [TASK-149](EPIC-013-multi-platform-clients/STORY-045-projects-homepage/TASK-149-home-dragon.md) Home Dragon on the projects homepage · FEATURE-026
  - STORY-046 Web UI: project workspace with Dragon docked — planned (3/3 done)
    - [x] [TASK-150](EPIC-013-multi-platform-clients/STORY-046-project-workspace-dragon/TASK-150-workspace-frame-spec.md) Project workspace frame with the read-only spec tab · FEATURE-026
    - [x] [TASK-151](EPIC-013-multi-platform-clients/STORY-046-project-workspace-dragon/TASK-151-dragon-dock.md) Dragon docked in the workspace, following the project · FEATURE-026
    - [x] [TASK-152](EPIC-013-multi-platform-clients/STORY-046-project-workspace-dragon/TASK-152-dragon-focus-resize.md) Dragon widens with focus · FEATURE-026
  - STORY-047 Web UI: workspace tabs — board, runs, git, stats — planned (9/9 done)
    - [x] [TASK-153](EPIC-013-multi-platform-clients/STORY-047-workspace-tabs/TASK-153-board-kanban.md) Board tab: status kanban with Retry · FEATURE-026
    - [x] [TASK-057](EPIC-013-multi-platform-clients/STORY-047-workspace-tabs/TASK-057-live-run-view.md) Runs tab: live run view · FEATURE-026
    - [x] [TASK-154](EPIC-013-multi-platform-clients/STORY-047-workspace-tabs/TASK-154-board-priority-lanes.md) Board: priority lanes in To do, drag to reprioritise · FEATURE-026
    - [x] [TASK-155](EPIC-013-multi-platform-clients/STORY-047-workspace-tabs/TASK-155-runs-list.md) Runs tab: the project's runs · FEATURE-026
    - [x] [TASK-156](EPIC-013-multi-platform-clients/STORY-047-workspace-tabs/TASK-156-git-branches-diff.md) Git tab: feature branches and their diff · FEATURE-026
    - [x] [TASK-157](EPIC-013-multi-platform-clients/STORY-047-workspace-tabs/TASK-157-git-merge.md) Git tab: merge a feature branch · FEATURE-026
    - [x] [TASK-158](EPIC-013-multi-platform-clients/STORY-047-workspace-tabs/TASK-158-stats-tab.md) Stats tab: per-project numbers · FEATURE-026
    - [x] [TASK-183](EPIC-013-multi-platform-clients/STORY-047-workspace-tabs/TASK-183-board-on-b-kanban.md) Board on Birko's b-kanban · FEATURE-026
    - [x] [TASK-184](EPIC-013-multi-platform-clients/STORY-047-workspace-tabs/TASK-184-runs-tab-pipeline-kobolds.md) Runs tab: list the pipeline's own Kobold runs · FEATURE-026
  - STORY-048 Web UI: notifications and decision cards — planned (2/2 done)
    - [x] [TASK-159](EPIC-013-multi-platform-clients/STORY-048-notifications-decisions/TASK-159-notifications.md) Notifications: header bell and project badges · FEATURE-026
    - [x] [TASK-160](EPIC-013-multi-platform-clients/STORY-048-notifications-decisions/TASK-160-decision-cards.md) Decision cards in Dragon · FEATURE-026
  - STORY-049 Web UI: settings — done (5/5 done)
    - [x] [TASK-161](EPIC-013-multi-platform-clients/STORY-049-settings/TASK-161-settings-providers.md) Settings: providers · FEATURE-026
    - [x] [TASK-162](EPIC-013-multi-platform-clients/STORY-049-settings/TASK-162-settings-api-keys.md) Settings: service-account API keys · FEATURE-026
    - [x] [TASK-192](EPIC-013-multi-platform-clients/STORY-049-settings/TASK-192-settings-models-default-agent-providers.md) Settings: a provider's models, the default provider, and which provider each agent uses · FEATURE-026
    - [x] [TASK-163](EPIC-013-multi-platform-clients/STORY-049-settings/TASK-163-settings-budgets.md) Settings: budgets · FEATURE-026
    - [x] [TASK-188](EPIC-013-multi-platform-clients/STORY-049-settings/TASK-188-theme-setting-follows-browser.md) The theme setting is applied, and "Auto" follows the browser's light/dark preference · FEATURE-026
  - STORY-050 Web UI: phone check-and-respond layout — done (1/1 done)
    - [x] [TASK-164](EPIC-013-multi-platform-clients/STORY-050-phone-layout/TASK-164-phone-check-and-respond.md) Phone check-and-respond layout · FEATURE-026
- **EPIC-014 Human-in-the-loop decision gates** — in-progress (9/11 tasks done)
  - STORY-024 Blocking "AwaitingHumanDecision" escalation tier — in-progress (2/3 done)
    - [x] [TASK-019](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/TASK-019-awaiting-human-decision-state.md) AwaitingHumanDecision state + HumanDecisionRequired disposition · FEATURE-027
    - [x] [TASK-020](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/TASK-020-drake-parks-routes-to-human.md) Drake parks task & routes decision to human instead of auto-resolving · FEATURE-027
    - [ ] [TASK-167](EPIC-014-human-in-the-loop-gates/STORY-024-blocking-decision-tier/TASK-167-pending-decisions-index-and-retention.md) Index the pending_decisions table and stop it growing without bound
  - STORY-025 Async `ask_human` tool for automatic agents — in-progress (6/6 done)
    - [x] [TASK-021](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-021-async-ask-human-tool.md) Implement async `ask_human` tool ⚠ blocked: merge deferred: stacked on task/TASK-020, which waits on its end-to-end check at TASK-024; code complete on task/TASK-021 · FEATURE-028
    - [x] [TASK-022](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-022-deliver-answer-into-context.md) Deliver human answer back into agent context on resume ⚠ blocked: merge deferred: stacked on task/TASK-021 (PR #3), itself stacked on task/TASK-020, which waits on its end-to-end check at TASK-024; code complete on task/TASK-022 · FEATURE-028
    - [x] [TASK-172](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-172-flaky-answers-on-summon-test.md) Find why a summoned Kobold sometimes hears no answers in the full test suite ⚠ blocked: merge deferred: stacked on task/TASK-025 (PR #6), unmerged; code complete on task/TASK-172 · FEATURE-028
    - [x] [TASK-175](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-175-answer-reaches-dependent-tasks.md) A task that depends on an answered question asks the person the same question again · FEATURE-028
    - [x] [TASK-171](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-171-ask-human-run-ending-edge-cases.md) ask_human: runs that end some other way as they ask · FEATURE-028
    - [x] [TASK-176](EPIC-014-human-in-the-loop-gates/STORY-025-ask-human-tool/TASK-176-forgiving-choice-answer-match.md) A typed answer that names an option is refused unless it repeats the option's whole text · FEATURE-028
  - STORY-026 Policy-driven decision gates (per-project) — planned (0/1 done)
    - [ ] [TASK-023](EPIC-014-human-in-the-loop-gates/STORY-026-decision-gate-policy/TASK-023-decision-gate-policy-config.md) DecisionGatePolicy config + enforcement in escalation routing · FEATURE-029
  - STORY-027 Answerable decisions surfaced in Dragon — in-progress (1/1 done)
    - [x] [TASK-024](EPIC-014-human-in-the-loop-gates/STORY-027-answerable-notifications/TASK-024-answerable-notifications-dragon.md) Answerable notifications + Dragon actionable prompt round-trip · FEATURE-030
- **EPIC-015 Self-reasoning depth & end-to-end verification** — in-progress (2/6 tasks done)
  - STORY-028 End-to-end task acceptance verification — in-progress (2/3 done)
    - [x] [TASK-025](EPIC-015-self-reasoning-depth/STORY-028-end-to-end-task-verification/TASK-025-task-acceptance-check.md) Post-plan task-acceptance check against acceptance criteria · FEATURE-031
    - [ ] [TASK-165](EPIC-015-self-reasoning-depth/STORY-028-end-to-end-task-verification/TASK-165-llm-judge-prose-acceptance-criteria.md) LLM judge for prose acceptance criteria, ahead of the human check ⚠ blocked: FEATURE-031 D6 deferred: until the acceptance check (TASK-025) has run and shown how many tasks wait on a person · FEATURE-031
    - [x] [TASK-173](EPIC-015-self-reasoning-depth/STORY-028-end-to-end-task-verification/TASK-173-acceptance-check-follow-ups.md) Acceptance check follow-ups: recovered reopens, the merge hold, a flaky test · FEATURE-031
  - STORY-029 Escalation loop closure — feed resolution back to the Kobold — planned (0/1 done)
    - [ ] [TASK-026](EPIC-015-self-reasoning-depth/STORY-029-escalation-loop-closure/TASK-026-feed-resolution-to-kobold.md) Feed escalation resolution back into Kobold context · FEATURE-032
  - STORY-030 Budget- and cost-aware reflection — planned (0/1 done)
    - [ ] [TASK-027](EPIC-015-self-reasoning-depth/STORY-030-budget-aware-reflection/TASK-027-budget-aware-reflection.md) Surface real token/cost spend into reflect tool & monitor · FEATURE-033
  - STORY-031 Cross-step coherence re-plan check — planned (0/1 done)
    - [ ] [TASK-028](EPIC-015-self-reasoning-depth/STORY-031-cross-step-replan-check/TASK-028-cross-step-coherence-check.md) Lightweight re-plan check when a step is revised/failed · FEATURE-034
- **EPIC-016 Database-backed agent working state** — in-progress (2/3 tasks done)
  - STORY-032 Migrate remaining file-based working artifacts to the database — in-progress (2/2 done)
    - [x] [TASK-094](EPIC-016-db-backed-agent-state/STORY-032-migrate-remaining-artifacts/TASK-094-remove-project-configs-json.md) Remove project-configs.json — the database is the only store for per-project agent settings
    - [x] [TASK-096](EPIC-016-db-backed-agent-state/STORY-032-migrate-remaining-artifacts/TASK-096-remove-dead-project-config-client.md) Remove the dead project-config-client.js (calls /api/project-configs routes nothing serves)
  - STORY-033 Persist structured decision & reasoning records — planned (0/0 done)
  - STORY-034 Codify and enforce the deliverable-vs-working-state boundary — planned (0/0 done)
  - (epic level) — 0/1 done
    - [ ] [TASK-200](EPIC-016-db-backed-agent-state/TASK-200-stores-work-on-any-database.md) Stores work on any database: SQLite for tests, PostgreSQL for production ⏳ waits on Birko.Framework/TASK-988
- **EPIC-017 Generalized agent consensus** — planned (0/0 tasks done)
  - STORY-035 Reusable ConsensusService abstraction — planned (0/0 done)
  - STORY-036 Diverse-panel consensus (heterogeneous agents) — planned (0/0 done)
  - STORY-037 Retrofit EPIC-010 voting integrations onto the shared mechanism — planned (0/0 done)
- **EPIC-018 Adopt Birko framework upstreams** — in-progress (12/29 tasks done)
  - STORY-038 Adopt the Birko security / OAuth upstreams — in-progress (2/4 done)
    - [x] [TASK-108](EPIC-018-adopt-birko-upstreams/STORY-038-adopt-birko-security-oauth/TASK-108-move-github-refresh-onto-birko-oauth-server.md) GitHub sign-in sessions are lost on every server restart — move refresh onto the Birko OAuth server · FEATURE-019
    - [x] [TASK-109](EPIC-018-adopt-birko-upstreams/STORY-038-adopt-birko-security-oauth/TASK-109-use-birko-oauth-server-stores.md) Use the framework OAuth server stores; delete SqliteOAuthStores and InMemoryOAuthStores · FEATURE-019
    - [ ] [TASK-110](EPIC-018-adopt-birko-upstreams/STORY-038-adopt-birko-security-oauth/TASK-110-use-birko-oauth-endpoint-mapping.md) Use the framework OAuth endpoint mapping and client-credentials hook; delete OAuthEndpoints and ServiceAccountTokenIssuer · FEATURE-019
    - [ ] [TASK-111](EPIC-018-adopt-birko-upstreams/STORY-038-adopt-birko-security-oauth/TASK-111-use-birko-web-sign-in-and-loopback-helpers.md) Use the framework GitHub web sign-in helpers and loopback bypass; delete the local copies · FEATURE-019
  - STORY-039 Adopt Birko.Web upstreams in the web client — in-progress (3/4 done)
    - [x] [TASK-112](EPIC-018-adopt-birko-upstreams/STORY-039-adopt-birko-web/TASK-112-fix-toasts-and-settings-tabs.md) Toasts show nothing and the settings tabs never render — both use Birko.Web components wrongly · FEATURE-026
    - [x] [TASK-113](EPIC-018-adopt-birko-upstreams/STORY-039-adopt-birko-web/TASK-113-use-birko-router-and-module-routes.md) Use the Birko.Web router with module routes and an auth guard; drop the hand-rolled router · FEATURE-026
    - [x] [TASK-114](EPIC-018-adopt-birko-upstreams/STORY-039-adopt-birko-web/TASK-114-one-token-store.md) One token store: the Shell auth store feeds WebSocket, REST and SSE; config.ts stops holding a token · FEATURE-026
    - ~~[TASK-115](EPIC-018-adopt-birko-upstreams/STORY-039-adopt-birko-web/TASK-115-replace-hand-rolled-widgets-with-birko-web.md) Replace hand-rolled widgets and helpers with Birko.Web ones~~ · FEATURE-026
    - ~~[TASK-116](EPIC-018-adopt-birko-upstreams/STORY-039-adopt-birko-web/TASK-116-adopt-birko-web-core-upstreams.md) Adopt WsClient.request and the runtime-config loader from Birko.Web.Core; delete the local copies~~ · FEATURE-026
    - [ ] [TASK-117](EPIC-018-adopt-birko-upstreams/STORY-039-adopt-birko-web/TASK-117-archive-stale-birko-web-docs.md) Archive the two stale Birko.Web docs (investigation report, shell fixes)
  - STORY-040 Adopt the Birko KoboldLair upstreams (agent runtime, data, server plumbing) — in-progress (7/20 done)
    - [x] [TASK-125](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-125-rebuild-kobold-loop-on-birko-agent-hooks.md) Rebuild Kobold step detection on Birko Agent hooks; delete the loop fork and the tool-wrapping
    - [x] [TASK-126](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-126-use-birko-orchestration-plan-model.md) Use Birko.AI.Orchestration's plan model, analyzer and dispatcher; migrate stored plan statuses
    - [x] [TASK-127](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-127-use-birko-sql-event-store.md) Use the framework SQL event store; delete SqlEventStoreRepository and DomainEventEntity
    - [x] [TASK-134](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-134-use-birko-websocket-endpoint-mapping.md) Map the WebSocket endpoints with Birko's mapping and RequireAuthorization; delete the three hand-written blocks
    - [x] [TASK-135](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-135-use-birko-websocket-connection.md) Receive whole messages with Birko's ReceiveMessageAsync; delete WebSocketSender and the hand-written receive loops
    - [x] [TASK-174](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-174-websocket-query-token-refused-on-kestrel.md) With JWT on, a real server refuses every `?token=` WebSocket connect
    - [x] [TASK-177](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-177-sqlite-repositories-use-wal.md) KoboldLair's SQLite repositories run on the rollback journal, where a read can silently come back empty
    - [ ] [TASK-122](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-122-use-birko-path-validator.md) Replace ExternalPathValidator with Birko's PathValidator / PathHelper.IsPathSafe
    - [ ] [TASK-128](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-128-persist-circuit-breaker-and-usage-via-birko.md) Persist circuit-breaker state and usage through Birko.AI.Resilience stores
    - [ ] [TASK-129](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-129-use-classified-llm-failures.md) Use classified LLM failures; retire ErrorClassifier's substring matching
    - [ ] [TASK-130](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-130-use-birko-secret-provider-and-cipher.md) Use the Birko config secret provider and field cipher; re-encrypt stored provider keys
    - [ ] [TASK-131](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-131-use-birko-live-event-broadcaster.md) Use the Birko keyed broadcaster for run events; delete KoboldRunEventSource
    - [ ] [TASK-132](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-132-use-birko-git-service-and-tools.md) Use the Birko git service and tools; delete GitService and the four git tools
    - [ ] [TASK-133](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-133-use-birko-conversation-checkpoints.md) Use Birko conversation checkpoints; fix restored turns reading back empty
    - [ ] [TASK-136](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-136-use-birko-background-jobs-hosting.md) Use Birko.BackgroundJobs hosting and registration; delete the two hosted-service bridges
    - [ ] [TASK-137](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-137-use-birko-websocket-command-router.md) Use the Birko command router for Dragon commands; keep the handlers
    - [ ] [TASK-124](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-124-export-birko-telemetry.md) ServiceDefaults does not export Birko's traces and metrics
    - [ ] [TASK-138](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-138-use-birko-sse-result.md) Stream run events with the Birko SSE result
    - [ ] [TASK-139](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-139-use-birko-ownership-check.md) Use the Birko owner-or-admin check; delete ApiOwnership
    - [ ] [TASK-140](EPIC-018-adopt-birko-upstreams/STORY-040-adopt-birko-koboldlair-upstreams/TASK-140-use-birko-websocket-proxy.md) Proxy /dragon and /wyvern in the client host with the Birko WebSocket proxy
  - STORY-044 Follow Birko framework contract changes — planned (0/1 done)
    - [ ] [TASK-079](EPIC-018-adopt-birko-upstreams/STORY-044-follow-birko-contract-changes/TASK-079-thread-the-cancellation-token-through-tool-bodies.md) Thread the cancellation token through the tool bodies
    - ~~[TASK-080](EPIC-018-adopt-birko-upstreams/STORY-044-follow-birko-contract-changes/TASK-080-rename-domainevententity-aggregateid.md) Rename DomainEventEntity.AggregateId to follow Birko's Guid naming rule~~
- **EPIC-019 KoboldLair review 2026-10** (review-intake) — in-progress (23/23 tasks done)
  - STORY-041 Security & tenancy — done (4/4 done)
    - [x] [TASK-118](EPIC-019-koboldlair-review-2026-10/STORY-041-security-tenancy/TASK-118-jwt-falls-back-to-a-public-secret.md) With no JWT secret configured, the server signs tokens with a secret published in the source — in every environment
    - [x] [TASK-119](EPIC-019-koboldlair-review-2026-10/STORY-041-security-tenancy/TASK-119-client-proxy-tls-and-token-leak.md) The client host's WebSocket proxy skips TLS validation, and `/api/config` hands the auth token to any caller
    - [x] [TASK-141](EPIC-019-koboldlair-review-2026-10/STORY-041-security-tenancy/TASK-141-rest-ownership-drops-non-guid-subjects.md) The /api/v1 ownership rule reads the caller as `ICurrentUser.UserGuid`, so GitHub and service-account callers own nothing
    - [x] [TASK-194](EPIC-019-koboldlair-review-2026-10/STORY-041-security-tenancy/TASK-194-key-name-can-reuse-an-untracked-client-identity.md) A new API key can take the name, and so the identity, of a client registered before key owners were recorded
  - STORY-042 Correctness & invariants — in-progress (18/18 done)
    - [x] [TASK-120](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-120-reflection-tool-static-state.md) ReflectionTool keeps the current plan, task and Kobold in static fields, so parallel Kobolds overwrite each other
    - [x] [TASK-168](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-168-worktree-removed-with-uncommitted-work.md) A task's worktree is force-removed after its run even when the work was never committed
    - [x] [TASK-121](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-121-shutdown-blocks-the-host-thread.md) Graceful shutdown blocks the host thread with Thread.Sleep inside ApplicationStopping
    - [x] [TASK-166](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-166-task-file-reads-failed-tasks-as-unassigned.md) A task file read back turns Failed and BlockedByFailure tasks into Unassigned, so they run again
    - [x] [TASK-169](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-169-monitor-escalations-routed-to-first-drake.md) Reasoning-monitor escalations go to the project's first Drake, not the one holding the task
    - [x] [TASK-178](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-178-verification-fix-tasks-never-run.md) Verification fix tasks are never run, and new ones are written every cycle
    - [x] [TASK-179](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-179-verification-checks-run-outside-the-workspace.md) Verification checks run outside the code they check, so a fixed project fails verification forever
    - [x] [TASK-180](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-180-chat-project-switch-keeps-old-context.md) A project switch made from the chat keeps the old project's conversation in Dragon's context
    - [x] [TASK-186](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-186-new-project-reuses-loaded-project-folder.md) A new project cannot be created once Dragon has loaded another project in the session
    - [x] [TASK-187](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-187-new-message-cancels-running-answer.md) A message sent while Dragon is still answering cancels that answer, and Dragon replies without it
    - [x] [TASK-190](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-190-dock-stays-on-project-after-chat-switch.md) After Dragon switches project from the chat, the web dock still shows the project that was opened · FEATURE-026
    - [x] [TASK-195](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-195-planner-result-shared-across-runs.md) Two planners running at once can take each other's plans
    - [x] [TASK-181](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-181-dragon-request-queue-dispose-throws.md) DragonRequestQueue.Dispose throws when it stops its workers
    - [x] [TASK-182](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-182-finished-task-escalated-as-stalled.md) A finished task is escalated as "progress stalled" and its plan revised after it is Done
    - [x] [TASK-189](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-189-flaky-auth-spec-config-route.md) An e2e auth test fails about one run in six: its config route is still fetching when the page closes
    - [x] [TASK-191](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-191-cancelled-run-logged-as-iteration-limit.md) A run stopped by a shutdown is logged as having reached its iteration limit
    - [x] [TASK-193](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-193-usage-records-name-provider-class.md) Usage records and rate limits name the provider class, not the configured provider
    - [x] [TASK-196](EPIC-019-koboldlair-review-2026-10/STORY-042-correctness-invariants/TASK-196-retry-in-completed-project-never-runs.md) A retried task of a Completed project is never run
  - STORY-043 Reuse & dead code — planned (1/1 done)
    - [x] [TASK-123](EPIC-019-koboldlair-review-2026-10/STORY-043-reuse-dead-code/TASK-123-dead-code-and-null-singletons.md) Clean-up from the KoboldLair review: dead code, `null!` singletons, in-memory aggregation
- **EPIC-020 Project lifecycle in KoboldLair** — planned (0/25 tasks done)
  - STORY-051 Questions climb the agent hierarchy — planned (0/10 done)
    - [ ] [TASK-201](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-201-a-question-climbs-the-agent-hierarchy-one-way-and.md) A question climbs the agent hierarchy one way, and its answer returns down the same path · FEATURE-078
    - [ ] [TASK-202](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-202-a-kobold-s-ask-human-goes-through-the-chain.md) A Kobold's ask_human goes through the chain ⏳ waits on TASK-201 · FEATURE-078
    - [ ] [TASK-203](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-203-drake-answers-a-kobold-s-question-from-what-its-le.md) Drake answers a Kobold's question from what its level owns, or passes it up ⏳ waits on TASK-201 · FEATURE-078
    - [ ] [TASK-204](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-204-wyvern-becomes-the-project-s-overseer-several-turn.md) Wyvern becomes the project's overseer: several turns, read-only tools ⏳ waits on TASK-170 · FEATURE-078
    - [ ] [TASK-205](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-205-wyvern-answers-a-question-from-the-specification-i.md) Wyvern answers a question from the specification, its analysis and the decisions, or passes it up ⏳ waits on TASK-201, TASK-204 · FEATURE-078
    - [ ] [TASK-206](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-206-dragon-answers-only-from-recorded-decisions-and-th.md) Dragon answers only from recorded decisions and the specification, otherwise asks the person to define it ⏳ waits on TASK-201 · FEATURE-078
    - [ ] [TASK-207](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-207-what-the-person-defines-is-written-into-the-specif.md) What the person defines is written into the specification as a new version ⏳ waits on TASK-206 · FEATURE-078
    - [ ] [TASK-208](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-208-an-answer-an-agent-gives-is-recorded-as-a-decision.md) An answer an agent gives is recorded as a decision the person can see and overturn ⏳ waits on TASK-201 · FEATURE-078
    - [ ] [TASK-209](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-209-questions-reach-the-person-in-rounds-of-at-most-fi.md) Questions reach the person in rounds of at most five, each with a recommended answer ⏳ waits on TASK-201 · FEATURE-078
    - [ ] [TASK-170](EPIC-020-project-lifecycle-in-koboldlair/STORY-051-questions-climb-the-hierarchy/TASK-170-ask-human-for-wyvern.md) ask_human for the analyzer agent (Wyvern) — needs a project-level wait · FEATURE-078
  - STORY-052 Lifecycle layer and records — planned (0/7 done)
    - [ ] [TASK-210](EPIC-020-project-lifecycle-in-koboldlair/STORY-052-lifecycle-layer-and-records/TASK-210-task-states-use-the-lifecycle-vocabulary.md) Task states use the lifecycle vocabulary · FEATURE-078
    - [ ] [TASK-211](EPIC-020-project-lifecycle-in-koboldlair/STORY-052-lifecycle-layer-and-records/TASK-211-the-lifecycle-layer-is-switched-on-per-project.md) The lifecycle layer is switched on per project · FEATURE-078
    - [ ] [TASK-212](EPIC-020-project-lifecycle-in-koboldlair/STORY-052-lifecycle-layer-and-records/TASK-212-the-plan-is-rendered-in-the-standard-lifecycle-lay.md) The plan is rendered in the standard lifecycle layout, read-only ⏳ waits on TASK-210, TASK-211 · FEATURE-078
    - [ ] [TASK-213](EPIC-020-project-lifecycle-in-koboldlair/STORY-052-lifecycle-layer-and-records/TASK-213-a-new-project-gets-the-full-lifecycle-layer-an-imp.md) A new project gets the full lifecycle layer; an imported one gets what it is missing ⏳ waits on TASK-211, TASK-212 · FEATURE-078
    - [ ] [TASK-214](EPIC-020-project-lifecycle-in-koboldlair/STORY-052-lifecycle-layer-and-records/TASK-214-dragon-s-interview-records-a-decision-ledger-per-f.md) Dragon's interview records a decision ledger per feature ⏳ waits on TASK-211 · FEATURE-078
    - [ ] [TASK-215](EPIC-020-project-lifecycle-in-koboldlair/STORY-052-lifecycle-layer-and-records/TASK-215-wyrm-records-the-project-s-conventions-vocabulary.md) Wyrm records the project's conventions, vocabulary and hard-to-reverse choices ⏳ waits on TASK-211 · FEATURE-078
    - [ ] [TASK-216](EPIC-020-project-lifecycle-in-koboldlair/STORY-052-lifecycle-layer-and-records/TASK-216-merging-a-feature-updates-the-project-changelog.md) Merging a feature updates the project changelog ⏳ waits on TASK-211 · FEATURE-078
  - STORY-053 Verify gate and self-healing — planned (0/7 done)
    - [ ] [TASK-217](EPIC-020-project-lifecycle-in-koboldlair/STORY-053-verify-gate-and-self-healing/TASK-217-the-acceptance-check-runs-checker-agents-on-every.md) The acceptance check runs checker agents on every task, cheapest first · FEATURE-078
    - [ ] [TASK-218](EPIC-020-project-lifecycle-in-koboldlair/STORY-053-verify-gate-and-self-healing/TASK-218-a-checker-confirms-a-task-built-what-was-asked.md) A checker confirms a task built what was asked ⏳ waits on TASK-217 · FEATURE-078
    - [ ] [TASK-222](EPIC-020-project-lifecycle-in-koboldlair/STORY-053-verify-gate-and-self-healing/TASK-222-checker-findings-go-back-to-the-worker-twice-at-mo.md) Checker findings go back to the worker, twice at most, then escalate ⏳ waits on TASK-217 · FEATURE-078
    - [ ] [TASK-219](EPIC-020-project-lifecycle-in-koboldlair/STORY-053-verify-gate-and-self-healing/TASK-219-a-checker-confirms-a-task-follows-the-project-s-co.md) A checker confirms a task follows the project's conventions ⏳ waits on TASK-217, TASK-215 · FEATURE-078
    - [ ] [TASK-220](EPIC-020-project-lifecycle-in-koboldlair/STORY-053-verify-gate-and-self-healing/TASK-220-a-checker-finds-stale-or-redundant-comments.md) A checker finds stale or redundant comments ⏳ waits on TASK-217 · FEATURE-078
    - [ ] [TASK-221](EPIC-020-project-lifecycle-in-koboldlair/STORY-053-verify-gate-and-self-healing/TASK-221-a-checker-looks-for-correctness-problems.md) A checker looks for correctness problems ⏳ waits on TASK-217 · FEATURE-078
    - [ ] [TASK-223](EPIC-020-project-lifecycle-in-koboldlair/STORY-053-verify-gate-and-self-healing/TASK-223-repeated-failures-and-monitor-alerts-become-tracke.md) Repeated failures and monitor alerts become tracked fix tasks, worked by impact · FEATURE-078
  - STORY-054 Following the lifecycle skills — planned (0/1 done)
    - [ ] [TASK-224](EPIC-020-project-lifecycle-in-koboldlair/STORY-054-following-the-skills/TASK-224-a-scheduled-ci-check-opens-an-issue-when-a-mirrore.md) A scheduled CI check opens an issue when a mirrored lifecycle skill changes · FEATURE-078

## Loose tasks

- [x] [TASK-073](_loose/TASK-073-db-backed-provider-config.md) Store LLM provider configuration in the database (editable, not files/env) (P1, ai)
- [x] [TASK-078](_loose/TASK-078-migrate-the-41-tool-overrides-to-the-token-signature.md) Migrate the 41 tool overrides — this repo has not compiled since 2026-07-09 (P1, ai)
- [x] [TASK-082](_loose/TASK-082-usage-records-missing-estimated-cost-column.md) Usage records are never saved: the usage_records table has no EstimatedCostUsd column (P1, ai)
- [x] [TASK-088](_loose/TASK-088-provider-registry-empty-for-dragon-on-fresh-server.md) Dragon fails on a fresh server: "Provider 'zai' is not registered" (empty provider registry) (P1, ai)
- [x] [TASK-089](_loose/TASK-089-project-provider-override-name-vs-type.md) Wyvern/Wyrm project provider overrides pass a provider name where a type is needed (P1, ai)
- [x] [TASK-090](_loose/TASK-090-new-projects-start-with-agents-disabled.md) A project created while the server runs is never analyzed: its agents start disabled (P1, ai)
- [x] [TASK-097](_loose/TASK-097-spec-path-lookup-fails-on-mixed-separators.md) Approved features never reach Wyvern when ProjectsPath uses forward slashes (spec-path lookup compares mixed separators) (P1, ai)
- [x] [TASK-098](_loose/TASK-098-empty-wyvern-reply-becomes-zero-task-analysis.md) An empty or unparseable Wyvern reply silently becomes a 0-task analysis and the project is marked Analyzed (P1, ai)
- [x] [TASK-076](_loose/TASK-076-birko-owned-package-versions-below-the-framework.md) Birko-owned package versions were below the framework, and one pin was holding a High advisory open (P2, ai)
- [x] [TASK-077](_loose/TASK-077-clear-the-two-high-advisories-deferred-by-task-076.md) Clear the two High advisories TASK-076 deferred (P2, ai)
- [x] [TASK-084](_loose/TASK-084-agent-provider-setting-names-missing-provider.md) A stored agent provider setting that names a missing provider breaks Dragon completely (P2, ai)
- [x] [TASK-086](_loose/TASK-086-zai-coding-plan-for-non-coding-agents.md) Z.AI: Dragon, Wyrm and Wyvern fail with "insufficient balance" and the model list is out of date (P2, ai)
- [x] [TASK-091](_loose/TASK-091-new-project-tasks-not-in-database.md) Tasks of a new project never reach the database, so the REST task endpoints can't see them (P2, ai)
- [x] [TASK-092](_loose/TASK-092-view-cost-report-tool-not-wired.md) The view_cost_report tool exists but no agent has it (P2, ai)
- [x] [TASK-093](_loose/TASK-093-deleted-project-wyvern-reused-by-name.md) A deleted project's Wyvern stays registered by name and is reused by a new project with that name (P2, ai)
- [x] [TASK-099](_loose/TASK-099-features-never-passed-to-wyvern.md) Approved features are never given to Wyvern — analysis sees only specification.md and features stay Ready (P2, ai)
- [x] [TASK-102](_loose/TASK-102-tasks-never-linked-to-features.md) Wyvern tasks are never linked to their features, so no work lands on feature branches; re-analysis of a new feature yields no task for it (P2, ai)
- [x] [TASK-103](_loose/TASK-103-delete-project-leaves-git-repo-and-task-rows.md) delete_project reports success but leaves a git project's folder and the project's task rows behind (P2, ai)
- [x] [TASK-104](_loose/TASK-104-test-host-loads-developer-appsettings-local.md) The API test host loads the developer's appsettings.local.json, so tests depend on the machine they run on (P2, ai)
- [x] [TASK-105](_loose/TASK-105-only-kobold-llm-calls-are-cost-tracked.md) Only Kobold LLM calls are cost-tracked — Dragon, the council, Wyrm, Wyvern and the planner are never recorded (P2, ai)
- [x] [TASK-106](_loose/TASK-106-planless-runs-emit-no-tool-call-events.md) A run without the enhanced plan path publishes no tool-call events — `/kobold` ad-hoc clients see no progress (P2, ai)
- [x] [TASK-081](_loose/TASK-081-decide-koboldlair-sqlite-reference.md) Decide whether DraCode.KoboldLair needs its own Microsoft.Data.Sqlite reference (P3, ai)
- [x] [TASK-083](_loose/TASK-083-run-status-stays-pending-without-plan.md) A run without a plan reports "pending" until it finishes — it never shows "running" (P3, ai)
- [x] [TASK-085](_loose/TASK-085-dragon-sends-specification-created-after-failed-request.md) Dragon sends specification_created after a request that failed (P3, ai)
- [x] [TASK-087](_loose/TASK-087-use-birko-ensurecolumns-in-usage-repository.md) Replace SqlUsageRepository's local add-missing-columns helper with Birko's EnsureColumns (P3, ai)
- [x] [TASK-101](_loose/TASK-101-diagnose-next-unusable-wyvern-reply.md) Find the root cause of empty Wyvern replies once the new log captures one (P3, ai)

<details><summary>Completed (1 epic)</summary>

- **EPIC-006 Self-reasoning — Option 2 (structured tools)** — done (2/2 tasks done)
  - STORY-003 Structured reasoning tools — done (2/2 done)
    - [x] [TASK-011](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-011-reflection-tool.md) ReflectionTool — structured reasoning capture · FEATURE-005
    - [x] [TASK-012](EPIC-006-self-reasoning-option-2/STORY-003-structured-reasoning-tools/TASK-012-reasoning-monitor-service.md) ReasoningMonitorService · FEATURE-005

</details>
