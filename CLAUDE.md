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
| `DraCode.Agent` | Core agent library with 17 specialized agents and 7 tools |
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
Wyrm (Automatic)         ← Analyzes specs, creates task breakdown (background service, 60s)
    ↓ Creates task files
Drake (Automatic)        ← Supervises task execution (background service, 60s)
    ↓ Assigns and monitors
Kobold Planner (Automatic) ← Creates implementation plans with atomic steps
    ↓ Plans ready for execution
Kobold (Automatic)       ← Executes plans step-by-step (per-project parallel limits)
```

- **Dragon**: Interactive chat for requirements → creates project folder and `specification.md`
  - Sub-agents: WardenAgent (security), LibrarianAgent (documentation), ArchitectAgent (design)
- **Wyrm**: Reads specs, breaks into tasks → creates `{area}-tasks.md` files
- **Drake**: Monitors tasks, summons Kobolds → updates task status
- **Kobold Planner**: Creates structured implementation plans → enables resumability
- **Kobold**: Executes plans step-by-step → outputs to `workspace/` subfolder

### Agent Types (17 total)

**Coding (12)**: `coding`, `csharp`, `cpp`, `assembler`, `javascript`, `typescript`, `css`, `html`, `react`, `angular`, `php`, `python`

**Media (3)**: `media`, `image`, `svg`, `bitmap` (bitmap extends image, image extends media)

**Other (2)**: `diagramming`, `wyrm`

### LLM Providers (10)

Located in `DraCode.Agent/LLMs/Providers/`:
- `OpenAiProvider` - OpenAI GPT models
- `ClaudeProvider` - Anthropic Claude models
- `GeminiProvider` - Google Gemini models
- `AzureOpenAiProvider` - Azure OpenAI Service
- `OllamaProvider` - Local Ollama server
- `LlamaCppProvider` - Local llama.cpp server (extends OpenAiCompatibleProviderBase)
- `GithubCopilotProvider` - GitHub Copilot API
- `ZAiProvider` - Z.AI (Zhipu) GLM models (glm-4.5, glm-4.6, glm-4.7)
- `VllmProvider` - vLLM local inference (extends OpenAiCompatibleProviderBase)
- `SglangProvider` - SGLang inference (extends OpenAiCompatibleProviderBase)

### Built-in Tools (7)

Located in `DraCode.Agent/Tools/`:
- `list_files` - Directory listing
- `read_file` - File reading
- `write_file` - File writing
- `search_code` - Code search with regex
- `run_command` - Shell command execution
- `ask_user` - User interaction
- `display_text` - Output display

### Dragon Tools (9)

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

### Kobold Planner Tool (1)

Located in `DraCode.KoboldLair/Agents/Tools/`:
- `create_implementation_plan` - Creates structured plans with atomic steps

## Key Technical Details

- **.NET 10.0**, C# 14.0, nullable reference types enabled
- **TypeScript 5.7** for DraCode.Web (ES2020 modules, zero runtime dependencies)
- Configuration in `appsettings.json` / `appsettings.Development.json`
- Providers disabled by default in base config, enabled per-environment
- **LLM Retry Logic**: All providers use exponential backoff with `SendWithRetryAsync`
  - Handles 429 (rate limiting), 5xx errors, timeouts, network failures
  - Respects `Retry-After` header; configurable via `RetryPolicy`

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
    projects.json                     # Project registry
    {sanitized-project-name}/         # Per-project folder (e.g., my-todo-app/)
        specification.md              # Project specification
        specification.features.json   # Feature list
        {area}-tasks.md               # Task files (e.g., backend-tasks.md)
        analysis.md                   # Wyvern analysis report
        workspace/                    # Generated code output
project-configs.json                  # Per-project kobold limits
provider-config.json                  # Provider configuration
user-settings.json                    # User runtime settings (agent providers)
```

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
4. `project-configs.json` - Per-project resource limits

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
4. **Kobold limit reached**: Check `maxParallelKobolds` in `project-configs.json`
