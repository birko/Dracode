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

8 projects in the solution (`DraCode.slnx`):

| Project | Purpose |
|---------|---------|
| `DraCode` | CLI console application (Spectre.Console) |
| `DraCode.Agent` | Core agent library with 17 specialized agents and 7 tools |
| `DraCode.AppHost` | .NET Aspire orchestration (service discovery, telemetry) |
| `DraCode.ServiceDefaults` | Shared Aspire configuration (health checks, resilience) |
| `DraCode.WebSocket` | WebSocket API server (`/ws` endpoint) |
| `DraCode.Web` | TypeScript web client (compiles `src/` → `wwwroot/js/`) |
| `DraCode.KoboldLair.Server` | Multi-agent autonomous coding backend |
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
Kobold (Automatic)       ← Code generation workers (per-project parallel limits)
```

- **Dragon**: Interactive chat for requirements → creates `./specifications/{project}_specification.md`
- **Wyrm**: Reads specs, breaks into tasks → creates `./tasks/{project}/*-tasks.md`
- **Drake**: Monitors tasks, summons Kobolds → updates task status
- **Kobold**: Executes code generation → outputs to `./workspace/{project}/`

### Agent Types (17 total)

**Coding (11)**: `coding`, `csharp`, `cpp`, `assembler`, `javascript`, `typescript`, `css`, `html`, `react`, `angular`, `php`, `python`

**Media (4)**: `media`, `image`, `svg`, `bitmap`

**Other (2)**: `diagramming`, `wyrm`

### LLM Providers (9)

Located in `DraCode.Agent/LLMs/`:
- `OpenAiProvider` - OpenAI GPT models
- `ClaudeProvider` - Anthropic Claude models
- `GeminiProvider` - Google Gemini models
- `AzureOpenAiProvider` - Azure OpenAI Service
- `OllamaProvider` - Local Ollama server
- `LlamaCppProvider` - Local llama.cpp server
- `GithubCopilotProvider` - GitHub Copilot API

### Built-in Tools (7)

Located in `DraCode.Agent/Tools/`:
- `list_files` - Directory listing
- `read_file` - File reading
- `write_file` - File writing
- `search_code` - Code search with regex
- `run_command` - Shell command execution
- `ask_user` - User interaction
- `display_text` - Output display

## Key Technical Details

- **.NET 10.0**, C# 14.0, nullable reference types enabled
- **TypeScript 5.7** for DraCode.Web (ES2020 modules, zero runtime dependencies)
- Configuration in `appsettings.json` / `appsettings.Development.json`
- Providers disabled by default in base config, enabled per-environment

### Environment Variables

```bash
# Required API keys (set based on which providers you use)
OPENAI_API_KEY=sk-...
ANTHROPIC_API_KEY=sk-ant-...
GOOGLE_API_KEY=...
AZURE_OPENAI_API_KEY=...
GITHUB_COPILOT_TOKEN=...

# Environment selection
ASPNETCORE_ENVIRONMENT=Development|Production
```

### WebSocket Endpoints

- `/wyvern` - Wyvern task delegation endpoint
- `/dragon` - Dragon requirements gathering chat
- Token auth: `ws://server/dragon?token=your-token`
- Keep-alive: 30 seconds

### Data Storage Locations

```
./projects/projects.json         # Project registry
./specifications/                # Generated specification files
./tasks/{projectId}/             # Task files per project
./workspace/{projectId}/         # Generated code output
project-configs.json             # Per-project kobold limits
provider-config.json             # Provider configuration
```

## Important Patterns

### Factory Pattern
- `AgentFactory.Create(provider, options, config, agentType)` - Creates specialized agents
- `AgentFactory` (in `Agents/`) - Creates Dragon, Wyrm, Drake agents with system prompts
- `KoboldFactory` (in `Factories/`) - Creates Kobolds with parallel limit enforcement
- `DrakeFactory` (in `Factories/`) - Creates and manages Drake supervisors
- `WyvernFactory` (in `Factories/`) - Creates Wyvern orchestrators

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
- `docs/Wyvern-Project-Analyzer.md` - Wyrm/Wyvern analyzer details
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
