# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build entire solution
dotnet build ./DraCode.slnx

# Run with .NET Aspire (recommended - starts all services with dashboard)
dotnet run --project DraCode.AppHost

# Run individual services
dotnet run --project DraCode.WebSocket          # WebSocket API on port 5000
dotnet run --project DraCode.Web                # Web client on port 5001
dotnet run --project DraCode.KoboldLair.Server  # KoboldLair backend
dotnet run --project DraCode.KoboldLair.Client  # KoboldLair web UI

# CLI agent with task
dotnet run --project DraCode -- --provider=openai --task="Your task here"

# Build TypeScript (DraCode.Web only)
cd DraCode.Web && npm run build
```

## Project Structure

9 projects in the solution (`DraCode.slnx`):

| Project | Purpose |
|---------|---------|
| `DraCode` | CLI console application (Spectre.Console) |
| `DraCode.Agent` | Core agent library with 23 agents organized into hierarchies and 7 tools |
| `DraCode.AppHost` | .NET Aspire orchestration (service discovery, telemetry) |
| `DraCode.ServiceDefaults` | Shared Aspire configuration (health checks, resilience) |
| `DraCode.WebSocket` | WebSocket API server (`/ws` endpoint) |
| `DraCode.Web` | TypeScript web client (compiles `src/` → `wwwroot/js/`) |
| `DraCode.KoboldLair` | Multi-agent system core library (agents, models, services, orchestrators) |
| `DraCode.KoboldLair.Server` | WebSocket server hosting for KoboldLair (references `DraCode.KoboldLair`) |
| `DraCode.KoboldLair.Client` | Multi-agent system web UI (vanilla JS) |

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
  - Dragon Council: SageAgent (specs/features), SeekerAgent (import), SentinelAgent (git), WardenAgent (config/retry)
- **Wyrm (Pre-Analysis)**: Reads specs, provides recommendations → creates `wyrm-recommendation.json` with languages, agent types, tech stack, complexity
  - **WyrmProcessingService** (60s): Monitors New projects, runs Wyrm pre-analysis, transitions to WyrmAssigned
- **Wyvern**: Reads specs + Wyrm recommendations, breaks into tasks → creates `{area}-tasks.md` files, persists `analysis.json`
  - **WyvernProcessingService** (60s): Monitors WyrmAssigned projects, runs detailed analysis, transitions to Analyzed
- **Drake**: Monitors tasks, summons Kobolds → updates task status
  - **DrakeExecutionService** (30s): Picks up analyzed projects, creates Drakes, summons Kobolds
  - **DrakeMonitoringService** (60s): Monitors stuck Kobolds, handles timeouts
- **Kobold Planner**: Creates structured implementation plans → enables resumability
- **Kobold**: Executes plans step-by-step → outputs to `workspace/` subfolder

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
- `ZAiProvider` - Z.AI (Zhipu) GLM models (glm-4.5, glm-4.6, glm-4.7) ✨ **Streaming**
- `VllmProvider` - vLLM local inference (extends OpenAiCompatibleProviderBase) ✨ **Streaming**
- `SglangProvider` - SGLang inference (extends OpenAiCompatibleProviderBase) ✨ **Streaming**

**All providers support streaming responses** for real-time token-by-token display.

### Built-in Tools (7)

Located in `DraCode.Agent/Tools/`:
- `list_files` - Directory listing
- `read_file` - File reading
- `write_file` - File writing (checks for existing files by default to prevent overwrites)
- `search_code` - Code search with regex
- `run_command` - Shell command execution
- `ask_user` - User interaction
- `display_text` - Output display

### Dragon Tools (15)

Located in `DraCode.KoboldLair/Agents/Tools/`:
- `list_projects` - List all registered projects
- `manage_specification` - Create, update, load specifications
- `manage_features` - Manage features within specifications
- `approve_specification` - Approve projects for processing
- `add_existing_project` - Register existing projects
- `git_status` - View branch status and merge readiness
- `git_merge` - Merge feature branches with conflict detection
- `manage_external_paths` - Add/remove allowed external paths
- `select_agent` - Select agent type for tasks
- `retry_analysis` - Retry failed Wyvern analysis (list/retry/status actions)
- `agent_status` - View running agents (Drakes, Kobolds) per project
- `retry_failed_task` - Retry failed tasks by resetting to Unassigned
- `set_task_priority` - Manually override task priority (critical/high/normal/low) to control execution order
- `pause_project` - Temporarily pause project execution (short-term hold)
- `resume_project` - Resume paused or suspended project
- `suspend_project` - Long-term hold for projects awaiting external changes
- `cancel_project` - Permanently cancel project (terminal state, requires confirmation)

### Kobold Planner Tool (1)

Located in `DraCode.KoboldLair/Agents/Tools/`:
- `create_implementation_plan` - Creates structured plans with atomic steps

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
- **TypeScript 5.7** for DraCode.Web (ES2020 modules, zero runtime dependencies)
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
    - **Critical**: Blocking tasks, infrastructure, project setup, README
    - **High**: Core features that are important but not blocking
    - **Normal**: Standard features and functionality (default)
    - **Low**: Nice-to-have features, polish, optional documentation
  - Dependencies always take precedence over priority

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
- **Cleanup**: Automatic cleanup timer runs every 60 seconds
- **Reconnection**: Sessions persist across disconnects; message history replayed on reconnect
- **Message Types**: `session_resumed`, `dragon_message`, `dragon_thinking`, `dragon_typing`, `specification_created`, `error`, `dragon_reloaded`

### Data Storage Locations (Consolidated Per-Project Folders)

The projects path is configurable via `appsettings.json` under `KoboldLair`:
- `ProjectsPath`: Where projects are stored (default: `./projects`)

```
{ProjectsPath}/                       # Configurable, defaults to ./projects
    projects.json                     # UNIFIED project registry (includes config + metadata)
    {sanitized-project-name}/         # Per-project folder (e.g., my-todo-app/)
        specification.md              # Project specification
        specification.features.json   # Feature list
        wyrm-recommendation.json      # Wyrm pre-analysis recommendations (NEW)
        analysis.md                   # Wyvern analysis report (human-readable)
        analysis.json                 # Wyvern analysis (machine-readable, persisted)
        tasks/                        # Task files subdirectory
            {area}-tasks.md           # Task files (e.g., backend-tasks.md)
        workspace/                    # Generated code output
        kobold-plans/                 # Implementation plans (NEW)
            {plan-filename}-plan.json # Machine-readable plan
            {plan-filename}-plan.md   # Human-readable plan
            plan-index.json           # Plan lookup index
        planning-context.json         # Shared planning context (NEW - 2026-02-09)
provider-config.json                  # Provider configuration
user-settings.json                    # User runtime settings (agent providers)
```

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

### Allowed External Paths

Per-project access control for directories outside workspace:
- Managed via `manage_external_paths` tool or `ProjectConfigurationService`
- Stored in project config (`AllowedExternalPaths` property)
- Kobolds inherit project's allowed paths during execution
- PathHelper validates all file operations against workspace + allowed paths

## Important Patterns

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

## Documentation Index

- `docs/README.md` - Documentation overview
- `docs/FULL_PROJECT_SPECIFICATION.md` - Complete spec for project regeneration
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

# Test WebSocket connection
curl http://localhost:5000/  # Health check
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
