# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build entire solution
dotnet build ./DraCode.slnx

# Run with .NET Aspire (recommended - starts all services with dashboard)
dotnet run --project DraCode.AppHost

# Run individual services
dotnet run --project DraCode.KoboldLair.Server  # KoboldLair backend
dotnet run --project DraCode.KoboldLair.Client  # KoboldLair web UI

# CLI agent with task
dotnet run --project DraCode -- --provider=openai --task="Your task here"

# Build TypeScript (DraCode.KoboldLair.Client)
cd DraCode.KoboldLair.Client && npm run build
```

## Project Structure

8 projects in the solution (`DraCode.slnx`):

| Project | Purpose |
|---------|---------|
| `DraCode` | CLI console application (Spectre.Console) |
| `DraCode.Birko` | Birko.Framework aggregator — pulls in the core agent library (Birko.AI agents, LLM providers, tools) for the solution |
| `DraCode.AppHost` | .NET Aspire orchestration (service discovery, telemetry) |
| `DraCode.ServiceDefaults` | Shared Aspire configuration (health checks, resilience) |
| `DraCode.KoboldLair` | Multi-agent system core library (agents, models, services, orchestrators) |
| `DraCode.KoboldLair.Server` | WebSocket server hosting for KoboldLair (references `DraCode.KoboldLair`) |
| `DraCode.KoboldLair.Client` | Multi-agent system web UI (TypeScript / Shadow DOM components) |
| `DraCode.KoboldLair.Tests` | Test suite for the KoboldLair system |

## Architecture

### KoboldLair Multi-Agent Hierarchy

```
Dragon (Interactive)     ← User's only touchpoint - requirements gathering
    ↓ Creates specification
Wyrm (Automatic)         ← Pre-analyzes specs, creates recommendations (WyrmProcessingService, 60s)
    ↓ Creates wyrm-recommendation.json
Wyvern (Automatic)       ← Analyzes specs, creates task breakdown (WyvernProcessingService, 60s)
    ↓ Creates task files + analysis.json (guided by Wyrm recommendations)
Drake (Automatic)        ← Supervises task execution (DrakeExecutionService, 30s)
    ↓ Creates Drakes, summons Kobolds
Kobold Planner (Automatic) ← Creates implementation plans with atomic steps
    ↓ Plans ready for execution
Kobold (Automatic)       ← Executes plans step-by-step (per-project parallel limits)
```

- **Dragon**: Interactive chat for requirements → creates project folder and `specification.md`
  - Dragon Council: SageAgent (specs/features/delete), SeekerAgent (import), SentinelAgent (git status/diff/commit/merge), WardenAgent (config/task details/progress/workspace/retry/delete)
- **Wyrm (Pre-Analysis)**: Reads specs, provides recommendations → creates `wyrm-recommendation.json` with languages, agent types, tech stack, complexity
  - WyrmRecommendation also includes `Constraints` (explicit spec restrictions) and `OutOfScope` (features explicitly excluded from scope)
  - **WyrmProcessingService** (60s): Monitors New projects, runs Wyrm pre-analysis, transitions to WyrmAssigned
- **Wyvern**: Reads specs + Wyrm recommendations, breaks into tasks → creates `{area}-tasks.md` files, persists `analysis.json`
  - WyvernAnalysis includes `Constraints`, `OutOfScope`, and `RequirementsCoverage` (maps spec requirements to task IDs)
  - **Requirements traceability**: Every spec requirement must map to at least one task
  - **Rich task descriptions**: Task descriptions must include acceptance criteria, target files, and public API signatures
  - **Task granularity guidelines**: Avoids shared dump files, splits large integration tasks
  - **WyvernProcessingService** (60s): Monitors WyrmAssigned projects, runs detailed analysis, transitions to Analyzed
- **Drake**: Monitors tasks, summons Kobolds → updates task status, manages git worktrees for feature branches
  - **DrakeExecutionService** (30s): Picks up analyzed projects, creates Drakes, summons Kobolds
  - **DrakeMonitoringService** (60s): Monitors stuck Kobolds, handles timeouts
  - **ReasoningMonitorService** (45s): Detects stuck loops, stalled progress, repeated errors, budget exhaustion → creates escalation alerts
  - **Escalation routing** (`HandleEscalationAsync`): Routes Kobold escalations to upstream agents:
    - `WrongApproach` → KoboldPlannerAgent.RevisePlanAsync (preserves completed steps, revises remaining)
    - `TaskInfeasible` / `NeedsSplit` / `MissingDependency` → Wyvern.RefineTaskAsync (LLM-driven task refinement)
    - `WrongAgentType` → task reset for reassignment with different agent
  - **Parked tasks** (TASK-019, FEATURE-027): `Drake.ParkTaskAsync` moves a task to `AwaitingHumanDecision` on a
    `PendingDecision` stored in the `pending_decisions` table (SQLite backend only; elsewhere parking is refused and the
    caller keeps the automatic path). The stored decision is the source of truth: Drake reconciles its tasks to it on
    every reload. A parked task is skipped by the work picker, its Kobold cannot overwrite it, and the monitors leave it
    alone; its area keeps a Drake. A decision's timeout is optional: `DrakeMonitoringService` expires overdue ones
    through the automatic escalation path, and one with no timeout waits until answered.
  - **Escalation to a person** (TASK-020): `HandleEscalationAsync(alert, disposition?)` asks an
    `IEscalationDispositionPolicy` (`DrakeFactory.DispositionPolicy`; the default keeps every escalation automatic until
    TASK-023's policy) and parks the task when it answers `HumanDecisionRequired`. A parked task absorbs further
    escalations unless a disposition is passed explicitly. Parking cancels the task's current run (each run in
    `ExecuteTaskAsync` registers its own cancellation) and keeps its worktree. An answer goes through
    `Drake.ResolvePendingDecisionAsync` / `DrakeFactory.ResolvePendingDecisionAsync`: "Handle it automatically" runs the
    escalation's automatic route, any other answer is recorded for the agent and the task resumes from its plan.
  - **ask_human** (TASK-021, FEATURE-028): Kobolds on a plan run get an `ask_human` tool (never Wyvern or Wyrm). The
    question (`AskHumanQuestion`: text, options, free-text flag, context) is stored as JSON on a `PendingDecision` with
    `Source = AskHuman`, and `Drake.AskHumanAsync` parks the task, refused when `IAskHumanPolicy`
    (`DrakeFactory.AskHumanPolicy`, default unrestricted) disallows it or the task's question cap is reached. Unlike an
    escalation, the run stops itself at its next turn boundary rather than at once, so on the enhanced plan path the
    question and its result end the saved conversation (the basic path saves only the plan). An answer to a question with options must be one of them unless free text is allowed;
    `ResolvePendingDecisionAsync` returns a `DecisionAnswerResult` (`InvalidAnswer` leaves the question open).
  - **Answers on resume** (TASK-022): Drake hands each summoned Kobold its task's answered and timed-out decisions
    (`Kobold.HumanAnswers`, oldest first). The Kobold hears each as a user message written as from a person and opening
    with `[Human answer — decision <id>]`, never as a tool result. A run resumed from its checkpoint gets only answers
    whose tag its restored conversation lacks (no "delivered" marker is stored), and a run with an answer waiting
    resumes its checkpoint even on the plan's first step. A fresh prompt, and the basic plan path, list every answer. A
    timeout is said as "no answer, timed out", with what the automatic route did (stored on the decision's escalation).
    When the decision store cannot be read, `SummonKoboldAsync` summons nothing and the task waits for a later cycle,
    rather than run without its answers. A task also hears what a person wrote for every task it depends on, directly
    or through others and in any area (TASK-175, FEATURE-028 D8): answers, typed escalation instructions, approval
    remarks and rejection reasons, never timeouts or "handle it automatically", each named as the other task's under
    its decision's tag. Such an inherited answer never makes a first-step run resume a checkpoint.
  - **Decision cards:** a park raises a `decision_required` project notification and an answer or timeout a
    `decision_resolved` one; the metadata keys and the response schema are `PendingDecisionNotification`'s, the
    contract the Dragon and web clients render and answer (TASK-024, TASK-160). `DecisionDesk` is the person's side
    (TASK-024): on `client_ready` the `/dragon` socket sends one `decision_required` card per open decision of the
    caller's projects, read from the decision store (not the notification list), so a card returns on every connect
    until answered or timed out. A new card, or its `decision_resolved`, goes to every session whose user can see the
    project. The client answers with `{ type: "answer_decision", decisionId, answer }` and gets
    `decision_answer_result` (`Resolved`, `AlreadyResolved`, `InvalidAnswer` or `NotFound`, with a message); a decision
    outside the caller's projects reads as `NotFound`. Warden's `pending_decisions` tool lists and answers them from
    the chat. The web dock (TASK-160) sends `client_ready` after each `project_selected` and resumed session; a
    `client_ready` after the welcome only asks for the cards and never reaches Dragon. `DecisionNotifications`
    (Server) raises and retires the cards' notifications: an answered decision's `decision_resolved` is kept read (the
    person did it), a timed-out one unread.
  - **Task state is Drake's** (TASK-153): a Drake's `TaskTracker` and the area's task file are what Drake works from;
    the task store (`ITaskRepository`) holds a copy the tracker writes to. Change a task through its Drake, or its file
    when no Drake holds it — `DrakeFactory.RetryFailedTaskAsync` does this for a retry and
    `DrakeFactory.SetTaskPriorityAsync` for a priority change (TASK-154), each for REST and Warden's tool. A change
    made only in the store is overwritten by the next save, or lost on a restart.
  - **Git Worktrees**: Creates isolated worktrees per feature branch for parallel-safe execution
  - **Post-task verification**: After task completion, runs Critical-priority verification steps (e.g., `tsc --noEmit`) from Wyrm recommendations
  - **Acceptance check** (TASK-025, FEATURE-031; off unless `KoboldLair:Verification:TaskAcceptanceEnabled`): before
    a finished task's Done reaches the WAL, `TaskAcceptanceService` holds it to what its own description promises —
    each target file exists and is not empty, each name in its `Exports:` clause is declared in those files. A broken
    promise reopens the task (`AcceptanceAttempts`, plan revised through the WrongApproach route, finished steps kept)
    up to `MaxAcceptanceReopens` (2); then it fails with a permanent error and a fix task names what is unmet. What no
    machine can judge (the "Must implement:" items, a clause that will not parse, a name found only in another file)
    parks the task on an `Approval` decision after committing its work: an answer starting "Approve" marks it Done (any
    remark kept on the decision), anything else rejects it with that text as the unmet criterion. A review card never
    times out, and the feature is not offered for merging while a task waits. Orphan recovery runs the same check.
  - **Constraints propagation**: Collects constraints from both Wyrm and Wyvern, displays prominently to Kobolds as "⛔ PROJECT CONSTRAINTS" block
- **Kobold Planner**: Creates structured implementation plans → enables resumability
  - **Module API signatures**: Receives extracted export statements from existing workspace files for cross-module awareness
  - **Plan revision**: `RevisePlanAsync` revises plans after escalation, preserving completed steps and generating new remaining steps
- **Kobold**: Executes plans step-by-step → outputs to `workspace/` subfolder
  - **Self-reflection**: `reflect` tool called every 3 iterations to report progress, confidence, blockers, and decision (continue/pivot/escalate)
  - **Escalation triggers**: Low confidence (<30%), stalled progress (3+ flat reflections), or explicit `escalate` decision
  - **Mandatory execution rules**: Read-before-write, no duplicate declarations, import consistency
  - **Integration task protocol**: Tasks with 4+ dependencies force reading all dependency files before writing
  - **Constraints display**: Project constraints shown prominently to prevent spec violations

**Note:** Wyrm also has a task delegation mode (WyrmAgent) used by Drake for selecting specialized agent types during execution.

### Agent Types (23 total)

**Base Classes**:
- `Agent` - Abstract base for all agents
- `OrchestratorAgent` - Base for orchestrators (Dragon, Wyrm, Wyvern) with helper methods:
  - `GetOrchestratorGuidance()` - Common orchestration best practices
  - `GetDepthGuidance()` - Model-specific reasoning instructions
  - `ExtractTextFromContent()` - Robust content parsing
  - `ExtractJson()` - JSON extraction from markdown
- `CodingAgent` - Base for coding-related agents
- `MediaAgent` - Base for media-related agents

**Coding Agents** (located in `DraCode.Agent/Agents/Coding/`):
- `coding` - General coding tasks
- `debug` - Debugging and troubleshooting
- `documentation` - Technical documentation
- `refactor` - Code refactoring
- `test` - Testing and test automation

**Specialized Coding Agents** (located in `DraCode.Agent/Agents/Coding/Specialized/`):
- `csharp`, `cpp`, `assembler` - Systems languages
- `javascript`, `typescript`, `css`, `html` - Web fundamentals
- `react`, `angular` - Web frameworks
- `php`, `python` - Scripting languages

**Media Agents** (located in `DraCode.Agent/Agents/Media/`):
- `media` - General media tasks
- `image` - Image processing (extends MediaAgent)
- `svg` - Vector graphics (extends ImageAgent)
- `bitmap` - Raster images (extends ImageAgent)

**Specialized Agents**:
- `diagramming` - UML, ERD, flowcharts
- `wyrm` - Task delegation orchestrator

### LLM Providers (10)

Located in `DraCode.Agent/LLMs/Providers/`:
- `OpenAiProvider` - OpenAI GPT models ✨ **Streaming**
- `ClaudeProvider` - Anthropic Claude models ✨ **Streaming**
- `GeminiProvider` - Google Gemini models ✨ **Streaming**
- `AzureOpenAiProvider` - Azure OpenAI Service ✨ **Streaming**
- `OllamaProvider` - Local Ollama server ✨ **Streaming**
- `LlamaCppProvider` - Local llama.cpp server (extends OpenAiCompatibleProviderBase) ✨ **Streaming**
- `GithubCopilotProvider` - GitHub Copilot API ✨ **Streaming**
- `ZAiProvider` - Z.AI (Zhipu) GLM models (glm-4.5, glm-4.6, glm-4.7) ✨ **Streaming** 🔧 **Coding Endpoint**
- `VllmProvider` - vLLM local inference (extends OpenAiCompatibleProviderBase) ✨ **Streaming**
- `SglangProvider` - SGLang inference (extends OpenAiCompatibleProviderBase) ✨ **Streaming**

**All providers support streaming responses** for real-time token-by-token display.

**Z.AI Coding Endpoint**: When using Z.AI with coding agents (csharp, python, javascript, etc.), the provider automatically routes requests to the specialized coding endpoint (`https://api.z.ai/api/coding/paas/v4/chat/completions`) for optimized code generation.

### Built-in Tools (7)

Located in `DraCode.Agent/Tools/`:
- `list_files` - Directory listing
- `read_file` - File reading
- `write_file` - File writing (checks for existing files by default to prevent overwrites)
- `search_code` - Code search with regex
- `run_command` - Shell command execution
- `ask_user` - User interaction
- `display_text` - Output display

### Dragon Tools (23)

Located in `DraCode.KoboldLair/Agents/Tools/`:
- `list_projects` - List all registered projects
- `manage_specification` - Create, update, load specifications
- `manage_features` - Manage features within specifications
- `delete_feature` - Delete Draft features from specifications (NEW - 2026-03-14)
- `view_specification_history` - View specification version history
- `approve_specification` - Approve projects for processing
- `add_existing_project` - Register existing projects
- `git_status` - View branch status, merge readiness, init repos
- `git_diff` - View branch diffs, commit logs, change summaries (NEW - 2026-03-14)
- `git_commit` - Stage and commit changes in project repos (NEW - 2026-03-14)
- `git_merge` - Merge feature branches with conflict detection
- `manage_external_paths` - Add/remove allowed external paths
- `select_agent` - Select agent type for tasks
- `retry_analysis` - Retry failed Wyvern analysis (list/retry/status actions)
- `agent_status` - View running agents (Drakes, Kobolds) per project
- `retry_failed_task` - Retry failed tasks by resetting to Unassigned
- `set_task_priority` - Manually override task priority (critical/high/normal/low) to control execution order
- `view_task_details` - View detailed task info, errors, plan step progress (NEW - 2026-03-14)
- `project_progress` - View project progress analytics, completion %, breakdowns (NEW - 2026-03-14)
- `view_workspace` - Browse generated output files in workspace (NEW - 2026-03-14)
- `delete_project` - Permanently remove cancelled projects from registry (NEW - 2026-03-14)
- `pause_project` - Temporarily pause project execution (short-term hold)
- `resume_project` - Resume paused or suspended project
- `suspend_project` - Long-term hold for projects awaiting external changes
- `cancel_project` - Permanently cancel project (terminal state, requires confirmation)
- `view_cost_report` - View LLM usage costs, budgets, and rate limit status (NEW - 2026-03-19)
- `pending_decisions` - List and answer the decisions agents wait on, for the user's projects (Warden, TASK-024)
- `batch_task_operations` - Batch task ops: list/reset blocked tasks, reassign agent type (NEW - 2026-03-19)

### Kobold Planner Tool (1)

Located in `DraCode.KoboldLair/Agents/Tools/`:
- `create_implementation_plan` - Creates structured plans with atomic steps

### Kobold Execution Tools (4)

Located in `DraCode.KoboldLair/Agents/Tools/`:
- `update_plan_step` - Mark plan steps as completed/failed/skipped (injected during execution)
- `reflect` - Self-assessment tool called every 3 iterations: reports progress, confidence, blockers, and decision (continue/pivot/escalate). Triggers escalation alerts on low confidence or stalled progress (NEW - 2026-03-15)
- `modify_plan` - Suggest plan modifications during execution (if enabled)
- `ask_human` - Ask a person a question on a plan run; parks the task until answered (TASK-021). Kobold only

### Shared Planning Context Service (NEW - 2026-02-09)

Located in `DraCode.KoboldLair/Services/`:
- `SharedPlanningContextService` - Comprehensive cross-agent coordination and learning system
  - **Multi-Agent Coordination**: Tracks active agents, detects file conflicts, finds related plans
  - **Drake Supervisor Support**: Agent lifecycle management, activity monitoring, project statistics
  - **Cross-Project Learning**: Records task metrics, analyzes patterns, provides insights
  - **Thread-Safe Design**: Concurrent dictionaries, file locking, LRU cache (max 50 projects)
  - **Persistence**: Auto-saves to `planning-context.json` per project on agent completion + shutdown
  - **Key Methods**:
    - `RegisterAgentAsync` / `UnregisterAgentAsync` - Lifecycle management
    - `IsFileInUseAsync` / `GetFilesInUseAsync` - File conflict detection
    - `GetRelatedPlansAsync` - Find plans touching similar files
    - `GetSimilarTaskInsightsAsync` - Learn from past executions (duration, steps, iterations)
    - `GetProjectStatisticsAsync` - Aggregate metrics (success rate, avg duration, active agents)
    - `GetCrossProjectInsightsAsync` - Learn from other projects
    - `GetBestPracticesAsync` - Extract patterns by agent type
  - **Documentation**: `docs/SharedPlanningContextService.md`

## Key Technical Details

- **.NET 10.0**, C# 14.0, nullable reference types enabled
- **TypeScript 5.7** for DraCode.KoboldLair.Client (ES2020 modules, Shadow DOM components)
- Configuration in `appsettings.json` / `appsettings.Development.json`
- Providers disabled by default in base config, enabled per-environment
- **LLM Retry Logic**: All providers use exponential backoff with `SendWithRetryAsync`
  - Handles 429 (rate limiting), 5xx errors, timeouts, network failures
  - Respects `Retry-After` header; configurable via `RetryPolicy`
- **Failure Recovery System** (added 2026-02-09)
  - Automatic retry of failed tasks with transient errors
  - Exponential backoff: 1min, 2min, 5min, 15min, 30min (max 5 retries)
  - Circuit breaker pattern: 3 failures = 10 minute pause
  - Error classification: transient (network) vs permanent (syntax)
  - Background service runs every 5 minutes
- **Streaming Support**: All 10 providers support real-time token streaming (added 2026-02-06)
  - Enabled by default for Dragon interactive chat
  - Automatic fallback to synchronous mode on streaming failures
  - WebSocket message type: `dragon_stream` with real-time chunk delivery
  - Client displays streaming text with animated cursor
- **Task Prioritization System** (added 2026-02-09)
  - Tasks assigned priority: Critical, High, Normal (default), Low
  - Wyvern automatically assigns priorities during analysis
  - Drake sorts tasks by: Priority DESC → Dependency Level → Complexity ASC
  - Manual override via Dragon tool: `set_task_priority`
  - Priority guidelines:
    - **Critical**: Blocking tasks, infrastructure, project setup
    - **High**: Core features that are important but not blocking
    - **Normal**: Standard features and functionality (default)
    - **Low**: Nice-to-have features, polish, documentation (README, etc.)
  - Dependencies always take precedence over priority
- **Specification Version Tracking** (added 2026-02-26)
  - Specifications track version number and SHA-256 content hash
  - Kobolds capture spec version when assigned, detect changes before execution
  - Automatic reload of specification context when version changes
  - Tasks record which spec version they were created for
  - Features file (`specification.features.json`) stores version metadata:
    ```json
    {
      "specificationVersion": 2,
      "specificationContentHash": "abc123...",
      "features": [...]
    }
    ```
  - Dragon tool: `view_specification_history` shows version history
  - Prevents specification drift during active project execution

### Streaming Configuration

Streaming can be configured per-agent via `AgentOptions`:

```csharp
var options = new AgentOptions 
{
    EnableStreaming = true,              // Enable streaming mode (default: false)
    StreamingFallbackToSync = true       // Auto-fallback on failure (default: true)
};
```

Dragon agent uses streaming by default for better UX. Streaming provides:
- **Lower perceived latency** - First tokens appear immediately
- **Better user experience** - Progressive text display feels more responsive
- **Seamless fallback** - Automatically uses non-streaming if provider doesn't support it

### Environment Variables

```bash
# Required API keys (set based on which providers you use)
OPENAI_API_KEY=sk-...
ANTHROPIC_API_KEY=sk-ant-...
GOOGLE_API_KEY=...
AZURE_OPENAI_API_KEY=...
GITHUB_COPILOT_TOKEN=...
ZHIPU_API_KEY=...                    # Z.AI provider

# Local inference servers (optional)
VLLM_BASE_URL=http://localhost:8000
SGLANG_BASE_URL=http://localhost:30000
LLAMACPP_BASE_URL=http://localhost:8080

# Environment selection
ASPNETCORE_ENVIRONMENT=Development|Production
```

### WebSocket Endpoints

- `/wyvern` - Wyvern task delegation endpoint
- `/dragon` - Dragon requirements gathering chat (multi-session support)
- Token auth: `ws://server/dragon?token=your-token`
- Keep-alive: 30 seconds

### Dragon Multi-Session Support

Dragon supports multiple concurrent sessions per WebSocket connection with automatic reconnection:
- **Session Management**: `ConcurrentDictionary<string, DragonSession>` tracks sessions
- **Session Timeout**: 10 minutes of inactivity before cleanup
- **Message History**: Up to 100 messages stored per session for replay
- **Conversation Persistence**: History saved per project folder (`Paths.Output`) in the `dragon_history` table
  (`SqlHistoryRepository`), or `dragon-history.json` without it; the user's messages and, since TASK-151, Dragon's
  replies (`DragonRequest.OnResponse`, recorded before the reply is sent)
- **Project Switch** (`select_project { projectId }`, TASK-151): the web dock sends it for the project the person opened.
  The server waits for the session's running request, then makes that project's saved conversation the session history
  and Dragon's LLM context (`ConversationFromHistory`: starts with the user, alternates, ends with Dragon), works in the
  project folder, and answers `project_selected` followed by the conversation replayed (`isReplay`). Only the caller's
  own projects; any other id gets an `error` with `errorType: project_not_found`. A selected project's conversation
  replaces the welcome
- **Home conversation** (`select_home`, TASK-149): the homepage's Dragon, with no project. The user's own conversation,
  stored per owner (`dragon_history` row `home:<sub>`, or `{ProjectsPath}/.home/<sub>/dragon-history.json`), answered
  with `home_selected` and replayed; an empty one starts with the welcome. While home, every message is saved there,
  even after the chat creates a project and Dragon's folder moves to it; the new project's own conversation starts
  empty. `specification_created` goes out after the project is registered and carries its `projectId`
- **Context Isolation**: both routes leave Dragon with only the new project's conversation. `select_project` (above)
  rebuilds it at once. The chat-driven route (Dragon's spec tool loading another project, `OnProjectLoadedAsync`) loads
  the new history and sets `PendingContextReset`; after the reply that announced the switch,
  `ApplyPendingContextReset` rebuilds the context from that history (TASK-180). Both key a project's history by its
  `Paths.Output`. In a workspace (a session whose client sent `select_project`, `SelectedByClient`) the chat route
  does not switch: the session keeps the opened project and the client gets `project_switch_offered { projectId,
  projectName }`, which the dock shows as "Open <name>" / "Stay here"; opening is the client's own `select_project`
  (TASK-190). A new spec Dragon writes there is offered the same way
- **One reply at a time** (TASK-187): `DragonRequestQueue` runs a session's messages in the order they came; one sent
  while Dragon is still answering is taken up when that answer ends (each request ends with `dragon_status`
  `complete` or `error`), and the dock shows it waiting. Sending never cancels; the cancel command drops only requests
  not yet started (a running one cannot be stopped)
- **No Client localStorage**: Client has no persistent storage for sessions; server is source of truth
- **Cleanup**: Automatic cleanup timer runs every 60 seconds
- **Reconnection**: Sessions persist across disconnects; message history replayed on reconnect (`?sessionId=`, which the
  client host's proxy forwards since TASK-151; a session the server no longer has gets `session_not_found`)
- **Message Types**: `session_resumed`, `dragon_message`, `dragon_thinking`, `dragon_typing`, `specification_created`, `error`, `dragon_reloaded`, `project_notification`, `decision_answer_result`, `project_selected`, `home_selected`, `project_switch_offered`; the client sends `select_project` to follow a project, `select_home` for the home conversation, and `answer_decision`
  to answer a decision card

### Data Storage Locations (Consolidated Per-Project Folders)

The projects path is configurable via `appsettings.json` under `KoboldLair`:
- `ProjectsPath`: Where projects are stored (default: `./projects`)

```
{ProjectsPath}/                       # Configurable, defaults to ./projects
    projects.json                     # UNIFIED project registry (includes config + metadata)
    {sanitized-project-name}/         # Per-project folder (e.g., my-todo-app/)
        specification.md              # Project specification
        specification.features.json   # Features + version metadata (wrapped format)
        wyrm-recommendation.json      # Wyrm pre-analysis recommendations
        analysis.md                   # Wyvern analysis report (human-readable)
        analysis.json                 # Wyvern analysis (machine-readable, persisted)
        tasks/                        # Task files subdirectory
            {area}-tasks.md           # Task files (e.g., backend-tasks.md)
        workspace/                    # Generated code output
        kobold-plans/                 # Implementation plans
            {plan-filename}-plan.json # Machine-readable plan
            {plan-filename}-plan.md   # Human-readable plan
            plan-index.json           # Plan lookup index
        planning-context.json         # Shared planning context
        dragon-history.json           # Dragon conversation history (server-persisted)
        notifications.json            # Project notifications: unread, plus the newest 50 read (TASK-159)
        .worktrees/                   # Git worktrees for parallel feature branch execution
            feature-abc-name/         # Isolated worktree per feature branch
provider-config.json                  # Provider configuration
user-settings.json                    # User runtime settings (agent providers)
```

### SQLite Database Runs in WAL (TASK-177)

Every store on `koboldlair.db` (the `Sql*Repository` classes, the event store, the OAuth stores) builds its settings
through `SqliteDatabaseSettings.For(dbPath)`, which returns `SqLiteSettings`, so Birko switches the file to WAL on first
use. Never pass plain `PasswordSettings`: Birko caches one connector per file and the first store decides the journal
mode for all, and on the rollback journal a pooled handle can read a table another connection created as empty, with no
error. `SqliteJournalModeTests` guards each repository.

- The mode is stored in the file and stays after the server stops.
- Recent commits live in `koboldlair.db-wal` beside the database, with `koboldlair.db-shm`. **A backup or copy must take
  all three files** (or run `PRAGMA wal_checkpoint(TRUNCATE)` first); copying only the `.db` can lose recent writes.
- WAL needs shared memory, so keep the database on a local disk, not a network share.

### Git Workflow (Automatic Feature Branch Management)

KoboldLair manages git automatically throughout the project lifecycle:

```
1. INIT (automatic)           ProjectService.CreateProjectFolderAsync()
   └→ git init -b main        When project folder is created

2. BRANCH (automatic)         Wyvern.AssignFeaturesAsync()
   └→ git branch feature/{id}-{name}    Per feature, from main

3. WORKTREE (automatic)       Drake.SetupFeatureBranchWorktreeAsync()
   └→ git worktree add .worktrees/{branch} {branch}
   └→ Kobold workspace → worktree/workspace/
   └→ Parallel-safe: each feature branch gets isolated copy

4. COMMIT (automatic)         Drake.CommitTaskCompletionAsync()
   └→ git add -A && git commit   In worktree (or main if no feature)
   └→ Author: Kobold-{agentType}
   └→ Conventional commit format with task metadata

5. CLEANUP (automatic)        Drake.ReleaseWorktreeAsync() (TASK-168)
   └→ git worktree remove       Only when nothing is lost: kept while its commit failed, its task waits on a
                                decision, another run of the feature uses it, or it holds uncommitted work (a run
                                stopped by a shutdown, a failed run); the feature's next run reuses it, and the run
                                that commits and leaves it clean removes it

6. NOTIFY (automatic)         ProjectNotificationService
   └→ Feature complete → notification persisted to notifications.json
   └→ Pushed to Dragon client (or on reconnect if offline)

7. MERGE (user-initiated)     Dragon → Sentinel → git_merge tool
   └→ User reviews via git_diff → merges via git_merge → deletes branch
```

**External Projects**: For imported codebases (`IsExistingProject=true`), git operations target the external `SourcePath` (e.g., `C:\Source\MyApp\`) instead of the KoboldLair metadata folder. Worktrees are created under the external repo's `.worktrees/` directory.

**Parallel Safety**: Multiple Drakes can work on different feature branches simultaneously. Each gets its own git worktree (isolated filesystem copy sharing the same `.git` object store). No branch checkout conflicts.

### Project Data Structure (projects.json)

Unified format combining project metadata and agent configuration:

```json
[
  {
    "id": "uuid-here",
    "name": "my-project",
    "status": "Analyzed",
    "executionState": "Running",
    "paths": {
      "specification": "./my-project/specification.md",
      "output": "./my-project/workspace",
      "analysis": "./my-project/analysis.json",
      "taskFiles": {
        "backend": "./my-project/tasks/backend-tasks.md"
      }
    },
    "timestamps": {
      "createdAt": "2026-02-04T00:00:00Z",
      "updatedAt": "2026-02-04T07:00:00Z",
      "analyzedAt": "2026-02-04T06:00:00Z",
      "lastProcessedAt": "2026-02-04T06:00:00Z"
    },
    "tracking": {
      "pendingAreas": [],
      "errorMessage": null,
      "lastProcessedContentHash": "abc123",
      "specificationId": "spec-uuid",
      "wyvernId": "wyvern-uuid"
    },
    "agents": {
      "wyrm": { "enabled": true, "provider": null, "model": null, "maxParallel": 1, "timeout": 0 },
      "wyvern": { "enabled": true, "provider": null, "model": null, "maxParallel": 1, "timeout": 0 },
      "drake": { "enabled": true, "provider": null, "model": null, "maxParallel": 1, "timeout": 0 },
      "koboldPlanner": { "enabled": true, "provider": null, "model": null, "maxParallel": 1, "timeout": 0 },
      "kobold": { "enabled": true, "provider": "zai", "model": null, "maxParallel": 4, "timeout": 1800 }
    },
    "security": {
      "allowedExternalPaths": [],
      "sandboxMode": "workspace"
    },
    "metadata": {}
  }
]
```


**Agent Config Fields:**
- `enabled` - Whether this agent is active for the project
- `provider` - LLM provider override (null = use global default)
- `model` - Model override (null = use provider default)
- `maxParallel` - Maximum concurrent instances of this agent type
- `timeout` - Timeout in seconds (0 = no timeout)

**Security Modes:**
- `workspace` - Only project workspace accessible (default)
- `relaxed` - Workspace + allowed external paths
- `strict` - Minimal access, explicit allowlist only

**Execution States:**
- `Running` - Normal execution, Drake processes tasks automatically (default)
- `Paused` - Temporarily halted, can be resumed at any time (short-term)
- `Suspended` - Long-term hold, requires explicit resume action
- `Cancelled` - Permanently stopped, terminal state (cannot be resumed)

**Execution Control:**
- Projects can be paused during high system load or debugging
- DrakeExecutionService only processes projects in `Running` state
- Execution state is independent of project status (e.g., InProgress but Paused)
- Use Warden's execution control tools: `pause_project`, `resume_project`, `suspend_project`, `cancel_project`
- Cancellation requires user confirmation and is permanent

### User Settings (user-settings.json)

Runtime settings that persist across restarts. Supports per-agent-type provider configuration for Kobolds:

```json
{
  "dragonProvider": "claude",
  "wyvernProvider": "openai",
  "koboldProvider": "openai",
  "koboldModel": null,
  "koboldAgentTypeSettings": [
    { "agentType": "csharp", "provider": "claude", "model": "claude-sonnet-4-20250514" },
    { "agentType": "python", "provider": "openai", "model": "gpt-4o" },
    { "agentType": "react", "provider": "gemini", "model": null }
  ]
}
```

**Resolution precedence for Kobold providers:**
1. `koboldAgentTypeSettings[agentType]` (if matching entry exists)
2. `koboldProvider` / `koboldModel` (global Kobold fallback)
3. `defaultProvider` (system default)

### Planning Configuration

Kobold Planner creates implementation plans before execution:

```json
{
  "KoboldLair": {
    "Planning": {
      "Enabled": true,
      "PlannerProvider": null,
      "PlannerModel": null,
      "MaxPlanningIterations": 5,
      "SavePlanProgress": true,
      "ResumeFromPlan": true
    }
  }
}
```

### Acceptance Check Configuration (TASK-025)

```json
{ "KoboldLair": { "Verification": { "TaskAcceptanceEnabled": false, "MaxAcceptanceReopens": 2 } } }
```

The same `Verification` section holds the toolchain settings `WyvernVerificationService` reads (`Enabled`,
`TimeoutSeconds`, `AutoCreateFixTasks`, …). `SqlTaskRepository` adds a column an older `tasks` table lacks at startup.

A failed project verification adds its fix tasks to the `verification-fixes` area through `VerificationFixTasks`
(TASK-178), via `TaskTracker` like any area, so Drake loads them from the JSON. One fix per check type stays open: a
re-run that fails again adds a fix only for a check whose earlier fix is Done. Every task file Drake writes goes through
`WriteTaskFileAsync`, which never saves an empty list over a file that holds tasks (`TaskTracker.FileHoldsTasks`):
Drake never removes tasks, so that save could only destroy ones it failed to load.

Verification checks run in the project's code folder (`ProjectWorkspace.For`: an imported project's `SourcePath`, else
`workspace/`), and a step's `WorkingDirectory` is relative to it (TASK-179). `VerificationShell` runs every step under
bash — `/bin/bash`, or Git for Windows' bash on Windows — since steps are written in POSIX shell; only a Windows machine
without Git bash falls back to PowerShell.

### Reflection Configuration (NEW - 2026-03-15)

Controls Kobold self-reflection and escalation behavior:

```json
{
  "KoboldLair": {
    "Reflection": {
      "Enabled": true,
      "EscalationConfidenceThreshold": 30,
      "StallDetectionCount": 3,
      "MonitorIntervalSeconds": 45,
      "NoProgressTimeoutMinutes": 10,
      "MaxFileWriteRepetitions": 3
    }
  }
}
```

- **EscalationConfidenceThreshold**: Confidence % below which `reflect` tool auto-escalates (default: 30)
- **StallDetectionCount**: Consecutive flat-progress reflections before auto-escalation (default: 3)
- **MonitorIntervalSeconds**: ReasoningMonitorService check interval (default: 45)
- **NoProgressTimeoutMinutes**: Minutes without step completion before monitor flags stall (default: 10)
- **MaxFileWriteRepetitions**: Repeated file writes before monitor flags stuck loop (default: 3)

### Project Notifications (TASK-159)

One stream, seen three ways (FEATURE-026 D17): `ProjectNotificationService` keeps each project's notifications in its
`notifications.json`; Dragon gets the unread ones over `/dragon`, and the web header bell and the homepage's project
badges read the same records through `GET /api/v1/notifications` (owner-scoped, `?scope=all` for an admin, unread first)
and `POST /api/v1/notifications/{id}/read` (404 for a project the caller cannot see). Reading one stamps `ReadAt` rather
than deleting it, so the bell can list it as read; each project keeps its newest 50 read ones. A project's file is loaded
before a new notification is added, so a restart no longer saves over the unread ones. The bell polls every 15 s while
the page is visible, and refreshes at once when a decision card in the dock is answered or retired (TASK-160). `GET /api/v1/needs-you`
(TASK-147) gathers what waits on the caller across their projects — open decisions, failed and blocked tasks, unread
`feature_branch_ready` notifications — for the homepage's "Needs you" list and the header pill.

### Settings → Providers, Agents and API keys (TASK-161, TASK-192, TASK-162)

`#/settings` shows what `GET /api/v1/settings/capabilities` allows: admins (`manage_config`) get a Providers tab, a Birko
Shell `BaseListPage` over `/api/v1/providers`. Those routes exist only when a provider master key is set
(`KoboldLair:ProviderConfig:MasterKey` or `KOBOLDLAIR_MASTER_KEY`, providers kept in the database); without one the tab
says so. Edits go through `PUT /providers/{name}`, API keys are write-only (`PATCH /providers/{name}/key`, shown only
as Set/Missing), and Validate (`POST /providers/{name}/validate`) makes one short request through the provider.
The Providers tab's "Models" action edits a provider's model list; the Agents tab (TASK-192, `#/settings/agents`) sets
the default provider, each orchestrator's provider and model, and per-Kobold-type overrides
(`GET /providers/agent-types` lists the types an override can name). It reloads when opened, and lists a provider that
is assigned but not available as "<name> (not available)".
The API keys tab (TASK-162, `#/settings/keys`) is for every signed-in user: `GET|POST /api/v1/keys`,
`POST /api/v1/keys/{id}/revoke` (mapped when `Authentication:OAuth:Enabled`). A key is a confidential client_credentials
OAuth client acting as `service:<name>` (names unique across every OAuth client, `/register`'s and older ones included, TASK-194), with view_own, manage_projects and execute_agents narrowed to its
creator's; its secret is returned once. Owners live in the `service_keys` table (`ServiceKeyRegistry`), as the OAuth
client store cannot be listed; `/register` records its admin as owner.

### Graceful Shutdown (TASK-121)

```json
{ "KoboldLair": { "Shutdown": { "TimeoutSeconds": 30 } } }
```

Sets `HostOptions.ShutdownTimeout` (default 30 s), the whole budget for stopping. On Ctrl+C the host cancels each
background service's stopping token and waits: `DrakeExecutionService`'s runs take that token, so a Kobold saves its
plan and its run ends before the service stops. `ShutdownFlushService`, registered before the background services
(hosted services stop in reverse order), then saves Drakes' pending task files, the planning contexts and the
notifications. No lifetime callback blocks or sleeps.

### WebSocket Message Cap

`/dragon`, `/wyvern` and `/kobold` read whole messages up to a cap:

```json
{ "KoboldLair": { "WebSocket": { "MaxMessageBytes": 4194304 } } }
```

- Default 4 MiB; allowed 16 KiB – 16 MiB. A value outside that range stops the server at startup.
- A larger message closes the socket with `MessageTooBig` and is never parsed. The cap bounds the memory one connection
  can make the server hold for an incoming message.

### Rate Limiting Configuration (NEW - 2026-03-19)

Per-provider rate limiting to prevent API quota exhaustion:

```json
{
  "KoboldLair": {
    "RateLimiting": {
      "Enabled": false,
      "ProviderLimits": [
        { "Provider": "openai", "RequestsPerMinute": 60, "TokensPerMinute": 150000, "RequestsPerDay": 0, "TokensPerDay": 0 }
      ]
    }
  }
}
```

### Cost Tracking Configuration (NEW - 2026-03-19)

LLM usage tracking with per-call token recording, cost estimation, and budget enforcement:

```json
{
  "KoboldLair": {
    "CostTracking": {
      "Enabled": true,
      "Pricing": [
        { "Provider": "openai", "Model": "gpt-4o", "InputPricePerMillionTokens": 2.50, "OutputPricePerMillionTokens": 10.00 },
        { "Provider": "claude", "Model": "*", "InputPricePerMillionTokens": 3.00, "OutputPricePerMillionTokens": 15.00 }
      ],
      "Budget": {
        "DailyBudgetUsd": 50,
        "MonthlyBudgetUsd": 500,
        "ProjectBudgetUsd": 100,
        "WarningThresholdPercent": 80
      }
    }
  }
}
```

- **Token Usage Extraction**: All 10 providers now return `TokenUsage` (prompt + completion tokens) in `LlmResponse`
- **TrackedLlmProvider**: Decorator that wraps any provider with rate limiting + cost tracking
- **Budget Enforcement**: Blocks LLM calls when daily/monthly/project budgets are exceeded
- **Dragon Tool**: `view_cost_report` with actions: summary, daily, project, budget, rate_limits
- **Storage**: Usage records persisted to SQLite via `SqlUsageRepository` (same DB as other entities)

### Allowed External Paths

Per-project access control for directories outside workspace:
- Managed via the `manage_external_paths` tool (Warden), persisted through `IProjectRepository`
- Stored on the project row (`Project.Security.AllowedExternalPaths`, `SecurityJson` column) — the database is the only store for per-project agent settings
- Kobolds inherit project's allowed paths during execution
- PathHelper validates all file operations against workspace + allowed paths

## Important Patterns

### Agent System Prompt Conventions

All agents in `DraCode.KoboldLair/Agents/` and `DraCode.Agent/Agents/` follow the same conventions inherited from `Birko.AI.Agent.cs`. Apply these when adding or editing any agent prompt; they are based on Anthropic's "Building Effective Agents" principles (simplicity, transparency, well-documented tools).

**Required interpolations** in the `SystemPrompt` getter (use `$@"..."` raw-interpolated strings):
- `{WorkingDirectory}` — if the agent uses file/shell tools, identify the sandboxed directory
- `{GetDepthGuidance()}` — virtual on `Agent`/`OrchestratorAgent`; tunes reasoning style based on `Options.ModelDepth` (1–10). Override the method, do NOT inline a `switch` in the prompt body
- `{GetFileOperationGuidelines()}` — include if the agent has any of `read_file` / `list_files` / `write_file` / `edit_file` / `append_to_file` / `search_code` tools
- `{GetCommonBestPractices()}` — include for agents that produce executable artifacts (code, tests); omit for pure-JSON analyzers or chat orchestrators where it doesn't apply

**Structure** (mirror `WyrmPreAnalysisAgent` as the exemplar):
1. One-line role statement with emoji and pipeline position
2. `{GetDepthGuidance()}` early (model sees reasoning style before content)
3. Process / responsibilities as a numbered list with a clear stop condition
4. Decision rules in priority order — be decisive, no "consider X then maybe Y"
5. Domain expertise as specific bullets (not "good at programming")
6. Closing instruction — what tool to call, or what format to emit

**Anti-patterns to avoid**:
- 🚫 Inline `Options.ModelDepth switch` in the prompt body — override `GetDepthGuidance()` instead
- 🚫 JSON output schemas inside `SystemPrompt` — put schemas in the user message (see `WyrmPreAnalysisAgent.AnalyzeSpecificationAsync`)
- 🚫 Tool descriptions duplicated in the prompt — tool docs live on the `Tool` class itself (Anthropic ACI principle)
- 🚫 Prompts over ~80 lines — attention dilutes; split into SystemPrompt (role + rules) + user message (data + format)
- 🚫 Plain `@"..."` when the prompt needs `{WorkingDirectory}` or any helper interpolation
- 🚫 Unreachable instructions like "test in different browsers" when the agent has no browser — write only steps the available tools can actually perform

**Inheritance**:
- `OrchestratorAgent` for agents that route or coordinate (Dragon, Wyrm, Wyvern) — gets `GetOrchestratorGuidance()`, `ExtractTextFromContent()`, `ExtractJson()`
- `Agent` (direct) for specialist workers and council sub-agents (Sage, Sentinel, Seeker, Warden, KoboldPlanner)
- `CodingAgent` / `MediaAgent` for language- or media-specialized agents (in `DraCode.Agent`)

Realignment audit was applied 2026-05-28 — see commit history for `DraCode.KoboldLair/Agents/`.

### Factory Pattern
- `AgentFactory.Create(provider, options, config, agentType)` - Creates specialized agents
- `AgentFactory` (in `Agents/`) - Creates Dragon, Wyrm, Drake agents with system prompts
- `KoboldFactory` (in `Factories/`) - Creates Kobolds with parallel limit enforcement
- `DrakeFactory` (in `Factories/`) - Creates and manages Drake supervisors
- `WyvernFactory` (in `Factories/`) - Creates Wyvern orchestrators
- `WyrmFactory` (in `Factories/`) - Creates Wyrm analyzers for specification processing

### Configuration Layering
1. `appsettings.json` - Base config with all providers disabled
2. `appsettings.{Environment}.json` - Enables environment-specific providers
3. Environment variables - API keys and secrets
4. `projects.json` - Per-project configuration (agent settings, security, metadata)

### JSON Serialization
- Incoming: `PropertyNameCaseInsensitive = true` (accepts camelCase from JS)
- Outgoing: `PropertyNamingPolicy.CamelCase` (sends camelCase to JS)

### Stored enums keep their numbers
An enum stored as a number (`TaskStatus` in the tasks table and the tracker JSON, the `PendingDecision` enums, the
framework's plan and escalation enums) has explicit values. Append new members with the next number; never renumber
or reorder.

## Testing

**Verification is an attribute of the feature/task being built — not a separate parallel tree.** Don't grow a stand-alone, module-indexed test-checklist tree disconnected from `docs/features/` and `tasks/`; that's a third axis that drifts. Instead:

- **Automated tests live in `tests/`** (`DraCode.KoboldLair.Tests` + any E2E) — runnable, version-controlled, the single source of "proof it works".
- **Layer automated coverage** (esp. for the web client), cheapest-first:
  1. **Generated smoke** — sweep every route/screen for "renders + no console errors + no failed requests", deriving the route list from the app's own manifest/router so it's **self-maintaining**.
  2. **Authored happy-path flows** — a few hand-written E2E flows for the important entities, reusing shared page objects.
  3. **Manual judgement** — only what a human must eye (copy, layout/feel). Keep these as a **coverage ledger** (each item tagged `[auto → spec]` / `[manual]`) or the task's `## Human test plan` — co-located with `tests/` or the task, not a free-floating doc tree.
- **Acceptance criteria belong on the feature** (`docs/features/FEATURE-NNN/`) and the task's Human test plan.
- **Done-gate:** done only when **automated tests pass *and* manual/acceptance checks were actually run** ("automated tests passing ≠ the feature works"). The generated smoke is the floor, not the ceiling.
- **Test data isolation** — run E2E against a **local / throwaway KoboldLair host**, never a shared or hosted one. The current smoke is read-only (`requireAuth:false`), but it still hits the host on mount, and once you wire authenticated/CRUD routes (see `tests/ui-e2e/README.md`) the specs mutate real data — point them at a disposable backend + a dedicated test account, never dev/prod. See the package README's *Test data isolation* section.

## Project Generation Guidelines

### Documentation Timing
- **README.md and similar project description files should be created LAST**, after all code is complete
- This ensures documentation accurately reflects the final project structure
- Wyvern/Kobolds should prioritize code implementation tasks before documentation tasks
- Documentation tasks should have **Low** priority to naturally execute last

## Documentation Index

- `docs/README.md` - Documentation overview
- `docs/architecture/` - Architecture and technical specifications
- `docs/setup-guides/` - Provider and feature setup guides
- `docs/troubleshooting/` - Troubleshooting guides
- `docs/Dragon-Requirements-Agent.md` - Dragon agent details
- `docs/Wyvern-Project-Analyzer.md` - Wyvern project analyzer details
- `docs/Drake-Monitoring-System.md` - Drake supervisor details
- `docs/Kobold-System.md` - Kobold worker details

## Testing

```bash
# Build and verify
dotnet build ./DraCode.slnx

# Run with Aspire dashboard for monitoring
dotnet run --project DraCode.AppHost
# Dashboard opens at https://localhost:17094
```

## Common Issues

1. **Provider not loading**: Check `ASPNETCORE_ENVIRONMENT` matches your config file
2. **API key not found**: Set environment variable for provider type (e.g., `ANTHROPIC_API_KEY`)
3. **WebSocket auth failed**: Verify token in query string matches server config
4. **Kobold limit reached**: Check `agents.kobold.maxParallel` in `projects.json`
5. **Network errors marking tasks as complete**: Fixed in v2.5.1 - update to latest version

## Error Handling

### LLM Provider Errors
- **Network Errors**: Retry logic with exponential backoff (default: 3 retries)
  - After retries exhausted, error is properly propagated to task status
  - Tasks marked as "Failed" instead of "Done" (fixed in v2.5.1)
- **Rate Limiting (429)**: Respects `Retry-After` header
- **Server Errors (5xx)**: Automatic retry with backoff
- **Configuration Errors**: Immediate failure with clear error message
- All providers use `SendWithRetryAsync` in `LlmProviderBase`

## Known Issues & Architectural Debt (2026-03-15)

28 execution pipeline gaps were fixed in commit `402dbc1`. 12 remaining items — all **resolved as of 2026-03-16** (see `git log` of the former `TODO.md`). Verified against current code 2026-05-28. The descriptions below capture the *original* issue shape; each line ends with how it was resolved.

### Concurrency Issues — resolved
- **Blocking sync-over-async** → Fully resolved as of TASK-018 (2026-05-29): `Tool` base class is `ExecuteAsync`-only (sync `Execute` removed; `ExecuteAsync` promoted to `abstract` in `Birko.AI.Contracts\Tools\Tool.cs`); all 23 DraCode tools + 9 Birko.AI tools migrated to async-only; `Kobold.cs:1344` second caller migrated to `await tool.ExecuteAsync(...)`; `AskUserTool`'s internal `Task.WhenAny+GetAwaiter().GetResult()` rewritten as proper `await`; DragonService callbacks remain `Func<..., Task>`. Zero `.GetAwaiter().GetResult()` in either DragonService or `Agents/Tools/` (DraCode or Birko.AI).
- **Git lock contention** → `static ConcurrentDictionary<string, SemaphoreSlim> _repoLocks` in `GitService.cs` — all git ops serialized per working directory.

### Data Persistence Issues — resolved by Birko.Data.SQL migration
- **Plan save debounce race** → `SqlPlanRepository` writes immediately on every `SavePlanAsync`; file debounce kept only for human-readable output.
- **Dragon history race** → `SqlHistoryRepository` with `SemaphoreSlim`-serialized writes.
- **Escalation persistence gap** → `ReflectionTool` now awaits `SavePlanAsync` (immediate, not debounced) before invoking the escalation callback.
- **Circuit breaker state lost** → `CircuitBreakerEntity` table; `ProviderCircuitBreaker.InitializePersistenceAsync()` rehydrates on startup.

### Git/Worktree Issues — resolved
- **Stale worktrees on restart** → `GitService.PruneStaleWorktreesAsync()` invoked from `Program.cs` startup, iterates all projects.
- **Commit failure silent** → `CommitFailed` bool added to `TaskRecord` / `TaskEntity` / `TaskViewModel` / `EntityMapper`; Drake sets it on commit failure.

### Client-Side Issues — resolved
- **Event listener leak** → moot in the new UI: dragon-view rewritten as a Shadow DOM `BaseComponent` (from `birko-web-core`), which handles connectedCallback/disconnectedCallback lifecycle automatically. Old vanilla-JS `dragon-view.js` (now in `wwwroot/archive/old-vanilla-ui/`) had the explicit `_handlers` / `detachEventListeners` fix.
- **Notification dedup** → resolved then by a `seenIds` Set in the old client store; that store was replaced in TASK-159 by the header bell's, which reads the server's list (`GET /api/v1/notifications`), so there is nothing to dedupe.

### Birko.Framework Migration Impact

The Birko.Data.SQL migration (PostgreSQL) resolved 4 of the 12 remaining issues by replacing file-based JSON storage with transactional database writes. Mapping:

| Birko Module | Resolves | Issues |
|--------------|----------|--------|
| `Birko.Data.SQL` (PostgreSQL) | Plan save race, history race, escalation persistence, circuit breaker state | 4 of 12 |
| `Birko.BackgroundJobs` | Partial help with worktree cleanup (startup job) | 1 of 12 |
| `Birko.Caching.Redis` / `Birko.EventBus` | Partial help with cross-branch API visibility, notification dedup | 2 of 12 |
| *Not applicable* | Tool async interface, git mutex, DragonService callbacks, event listener leak, commit failure propagation | 5 of 12 |
