# DraCode — Features Index

The human entry point to the feature list. Each row links to a `FEATURE-NNN-slug/`
folder holding `idea.md` (problem + shape), `decisions.md` (the decision ledger), and
`status.md` (the PM/stocktaker rollup). This index was seeded on **2026-05-31** from the
planned `tasks/` tree (forward-looking work) and from `docs/CHANGELOG.md` (shipped work).

Lifecycle (per the `feature` skill): `new → prototype → decide → decompose → (work in tasks/) → status → review`.
Run `/feature <verb>` to advance a feature; run bare `/feature` for the live computed view.

**Status legend:** `idea` = captured, not yet built · `review` = built, sign-off pending · `done` = shipped & signed off.

> ⚠️ Most planned features below are seeded as `idea` with **`proposed`** decisions — they
> have **not** been through `/feature decide` yet. A stakeholder still needs to approve /
> defer / drop each decision before `/feature decompose` generates any new tasks. The
> existing `tasks/` are back-linked (`feature: FEATURE-NNN`) but were authored before this index.
>
> **Exception (decided 2026-06-17):** FEATURE-017, FEATURE-018, FEATURE-019, and FEATURE-020
> have been through `/feature decide` — all their decisions are `approved` and they are in
> phase `building` (coarse status stays `idea` until built/signed off; see each `status.md`).

---

## Planned / in-flight (from `tasks/`)

| Feature | Title | Status | Source |
|---------|-------|--------|--------|
| [FEATURE-001](FEATURE-001-token-storage-auth/idea.md) | Token storage & auth providers | idea | EPIC-001 |
| [FEATURE-002](FEATURE-002-plugin-extensibility/idea.md) | Plugin system for custom tools | idea | EPIC-002 |
| [FEATURE-003](FEATURE-003-enterprise-team-compliance/idea.md) | Enterprise features (team, audit, CI/CD) | idea | EPIC-004 |
| [FEATURE-004](FEATURE-004-monitoring-infra/idea.md) | Monitoring & observability infrastructure | idea | EPIC-005 |
| [FEATURE-005](FEATURE-005-structured-reasoning-tools/idea.md) | Structured reasoning tools | done | EPIC-006 |
| [FEATURE-006](FEATURE-006-research-advanced-ml/idea.md) | Advanced ML research (RAG + fine-tuned models) | idea | EPIC-008 |
| [FEATURE-007](FEATURE-007-merge-wyrm-wyvern/idea.md) | Evaluate merging Wyrm + Wyvern | idea | EPIC-009 |
| [FEATURE-008](FEATURE-008-fold-koboldplanner-into-kobold/idea.md) | Evaluate folding KoboldPlanner into Kobold | idea | EPIC-009 |
| [FEATURE-009](FEATURE-009-collapse-drake-services/idea.md) | Evaluate collapsing the Drake services | idea | EPIC-009 |
| [FEATURE-010](FEATURE-010-wyvern-task-breakdown-voting/idea.md) | Wyvern task-breakdown voting | idea | EPIC-010 |
| [FEATURE-011](FEATURE-011-wyrmagent-selection-voting/idea.md) | WyrmAgent agent-type selection voting | idea | EPIC-010 |
| [FEATURE-012](FEATURE-012-koboldplanner-plan-voting/idea.md) | KoboldPlanner plan voting | idea | EPIC-010 |
| [FEATURE-013](FEATURE-013-design-evaluator-agent/idea.md) | Design EvaluatorAgent | idea | EPIC-011 |
| [FEATURE-014](FEATURE-014-integrate-evaluator-into-kobold/idea.md) | Integrate EvaluatorAgent into the Kobold loop | idea | EPIC-011 |
| [FEATURE-015](FEATURE-015-measure-evaluator-vs-reflect/idea.md) | Measure: evaluator vs reflect | idea | EPIC-011 |
| [FEATURE-016](FEATURE-016-retire-old-stack/idea.md) | Retire DraCode.WebSocket & DraCode.Web | done | EPIC-012 |
| [FEATURE-017](FEATURE-017-kobold-endpoint/idea.md) | /kobold WebSocket endpoint | idea | EPIC-012 |
| [FEATURE-018](FEATURE-018-rest-sse-facade/idea.md) | REST + SSE facade | idea | EPIC-012 |
| [FEATURE-019](FEATURE-019-oauth-identity/idea.md) | OAuth/OIDC identity | idea | EPIC-012 |
| [FEATURE-020](FEATURE-020-daemon-mode/idea.md) | Daemon mode for KoboldLair.Server | idea | EPIC-012 |
| [FEATURE-021](FEATURE-021-remove-sync-tool-execute/idea.md) | Remove sync Tool.Execute() overloads | done | EPIC-012 |
| [FEATURE-077](FEATURE-077-unify-framework-source-compilation/idea.md) | Unify Birko framework source compilation | done | EPIC-012 |
| [FEATURE-022](FEATURE-022-cli-client/idea.md) | CLI client | idea | EPIC-013 |
| [FEATURE-023](FEATURE-023-discord-bot/idea.md) | Discord bot client | idea | EPIC-013 |
| [FEATURE-024](FEATURE-024-vscode-extension/idea.md) | VSCode extension client | idea | EPIC-013 |
| [FEATURE-025](FEATURE-025-python-sdk/idea.md) | Python SDK | idea | EPIC-013 |
| [FEATURE-026](FEATURE-026-web-client-polish/idea.md) | Web client UI polish | idea | EPIC-013 |
| [FEATURE-027](FEATURE-027-blocking-decision-tier/idea.md) | Blocking AwaitingHumanDecision tier | idea | EPIC-014 |
| [FEATURE-028](FEATURE-028-ask-human-tool/idea.md) | Async ask-human tool | idea | EPIC-014 |
| [FEATURE-029](FEATURE-029-decision-gate-policy/idea.md) | Policy-driven decision gates | idea | EPIC-014 |
| [FEATURE-030](FEATURE-030-answerable-notifications/idea.md) | Answerable decisions in Dragon | idea | EPIC-014 |
| [FEATURE-031](FEATURE-031-end-to-end-task-verification/idea.md) | End-to-end task acceptance verification | idea | EPIC-015 |
| [FEATURE-032](FEATURE-032-escalation-loop-closure/idea.md) | Escalation loop closure | idea | EPIC-015 |
| [FEATURE-033](FEATURE-033-budget-aware-reflection/idea.md) | Budget-aware reflection | idea | EPIC-015 |
| [FEATURE-034](FEATURE-034-cross-step-replan-check/idea.md) | Cross-step coherence re-plan check | idea | EPIC-015 |
| [FEATURE-035](FEATURE-035-migrate-remaining-artifacts/idea.md) | Migrate remaining artifacts to DB | idea | EPIC-016 |
| [FEATURE-036](FEATURE-036-persist-decisions-reasoning/idea.md) | Persist decision & reasoning records | idea | EPIC-016 |
| [FEATURE-037](FEATURE-037-codify-deliverable-boundary/idea.md) | Codify deliverable-vs-working boundary | idea | EPIC-016 |
| [FEATURE-038](FEATURE-038-reusable-consensus-mechanism/idea.md) | Reusable ConsensusService abstraction | idea | EPIC-017 |
| [FEATURE-039](FEATURE-039-diverse-panel-consensus/idea.md) | Diverse-panel consensus | idea | EPIC-017 |
| [FEATURE-040](FEATURE-040-retrofit-voting-integrations/idea.md) | Retrofit voting integrations | idea | EPIC-017 |

## Shipped (backfilled from `docs/CHANGELOG.md`)

| Feature | Title | Status | Version |
|---------|-------|--------|---------|
| [FEATURE-041](FEATURE-041-pipeline-quality-improvements/idea.md) | Pipeline quality improvements | done | Unreleased |
| [FEATURE-042](FEATURE-042-specification-version-tracking/idea.md) | Specification version tracking | done | Unreleased |
| [FEATURE-043](FEATURE-043-shared-planning-context-service/idea.md) | Shared planning context service | done | Unreleased |
| [FEATURE-044](FEATURE-044-wyrm-pre-analysis-workflow/idea.md) | Wyrm pre-analysis workflow | done | 2.6.0 |
| [FEATURE-045](FEATURE-045-agent-creation-pattern-consistency/idea.md) | Agent creation pattern consistency | done | 2.6.0 |
| [FEATURE-046](FEATURE-046-network-error-handling/idea.md) | Network error handling for tasks | done | 2.5.1 |
| [FEATURE-047](FEATURE-047-orchestrator-agent-base/idea.md) | OrchestratorAgent base class | done | 2.5.0 |
| [FEATURE-048](FEATURE-048-agent-organization-namespaces/idea.md) | Agent folder reorganization & namespaces | done | 2.5.0 |
| [FEATURE-049](FEATURE-049-parallel-execution-threading/idea.md) | Parallel execution & threading | done | 2.4.2 |
| [FEATURE-050](FEATURE-050-sectioned-project-config/idea.md) | Sectioned project configuration | done | 2.4.2 |
| [FEATURE-051](FEATURE-051-drake-execution-service/idea.md) | Drake execution service | done | 2.4.1 |
| [FEATURE-052](FEATURE-052-wyvern-analysis-persistence/idea.md) | Wyvern analysis persistence | done | 2.4.1 |
| [FEATURE-053](FEATURE-053-retry-analysis-tool/idea.md) | Retry analysis tool | done | 2.4.1 |
| [FEATURE-054](FEATURE-054-performance-optimizations/idea.md) | Performance optimizations | done | 2.4.1 |
| [FEATURE-055](FEATURE-055-wyvernagent-json-handling/idea.md) | Robust WyvernAgent JSON handling | done | 2.4.1 |
| [FEATURE-056](FEATURE-056-v241-bug-fixes/idea.md) | v2.4.1 bug fixes | done | 2.4.1 |
| [FEATURE-057](FEATURE-057-kobold-implementation-planner/idea.md) | Kobold implementation planner | done | 2.4.0 |
| [FEATURE-058](FEATURE-058-allowed-external-paths/idea.md) | Allowed external paths | done | 2.4.0 |
| [FEATURE-059](FEATURE-059-llm-retry-logic/idea.md) | LLM retry logic with backoff | done | 2.4.0 |
| [FEATURE-060](FEATURE-060-dragon-council-subagents/idea.md) | Dragon Council sub-agents | done | 2.4.0 |
| [FEATURE-061](FEATURE-061-git-integration/idea.md) | Git integration | done | 2.3.0 |
| [FEATURE-062](FEATURE-062-thinking-indicator/idea.md) | Dragon thinking indicator | done | 2.3.0 |
| [FEATURE-063](FEATURE-063-new-specialized-agent-types/idea.md) | New specialized agent types (PHP, Python, Media) | done | 2.2.2 |
| [FEATURE-064](FEATURE-064-new-llm-providers/idea.md) | New LLM providers (Z.AI, vLLM, SGLang) | done | 2.2.0 |
| [FEATURE-065](FEATURE-065-dragon-enhancement-tools/idea.md) | Dragon enhancement tools (import & approval) | done | 2.2.0 |
| [FEATURE-066](FEATURE-066-model-reorganization/idea.md) | Model reorganization | done | 2.2.0 |
| [FEATURE-067](FEATURE-067-ui-improvements/idea.md) | Web UI improvements | done | 2.2.0 |
| [FEATURE-068](FEATURE-068-dragon-multi-session-support/idea.md) | Dragon multi-session support | done | 2.2.0 |
| [FEATURE-069](FEATURE-069-websocket-auth-ip-binding/idea.md) | WebSocket authentication with IP binding | done | 2.0.5 |
| [FEATURE-070](FEATURE-070-multiple-provider-connections/idea.md) | Multiple connections to same provider | done | 2.0.4 |
| [FEATURE-071](FEATURE-071-clickable-links-activity-log/idea.md) | Clickable links in activity log | done | 2.0.3 |
| [FEATURE-072](FEATURE-072-websocket-multi-agent-system/idea.md) | WebSocket multi-agent system | done | 2.0 |
| [FEATURE-073](FEATURE-073-web-client-modernization/idea.md) | Web client modernization | done | 2.0.1 |
| [FEATURE-074](FEATURE-074-v202-bug-fixes/idea.md) | v2.0.2 bug fixes & debug tooling | done | 2.0.2 |
| [FEATURE-075](FEATURE-075-multi-task-execution/idea.md) | Multi-task sequential execution | done | 2.1 |
| [FEATURE-076](FEATURE-076-initial-release/idea.md) | Initial release | done | 1.0 |

---

_Next `FEATURE-NNN` id: **FEATURE-078**. Add new features with `/feature new`._
