# DraCode Documentation

Welcome to the DraCode documentation. This directory contains all technical documentation, guides, and specifications.

## Quick Links

- **[Full Project Specification](FULL_PROJECT_SPECIFICATION.md)** - Complete spec for regenerating the project
- **[Changelog](CHANGELOG.md)** - Version history and release notes (v2.4.1 - February 2026)
- **[KoboldLair Core Library](../DraCode.KoboldLair/README.md)** - Multi-agent orchestration library
- **[KoboldLair Server](../DraCode.KoboldLair.Server/README.md)** - Multi-agent backend
- **[KoboldLair Client](../DraCode.KoboldLair.Client/README.md)** - Multi-agent web UI

## Latest Updates (v2.5.1)

- **Network Error Handling Fix**: Critical bug fixed where network errors incorrectly marked tasks as complete
  - Properly fails Kobold tasks on network/provider errors
  - Error messages injected into conversation for `"error"` and `"NotConfigured"` stop reasons
  - Applies to all 10 LLM providers and all agent types
- **OrchestratorAgent Base Class** (v2.5.0): New abstract base for Dragon, Wyrm, Wyvern with shared helper methods
  - `GetOrchestratorGuidance()`, `GetDepthGuidance()`, `ExtractTextFromContent()`, `ExtractJson()`
  - Reduces code duplication, improves maintainability
- **Agent Reorganization** (v2.5.0): 23 agents organized into hierarchical folder structure
  - `Agents/` (6 base classes) + `Coding/` (4) + `Coding/Specialized/` (10) + `Media/` (3)
  - New namespaces: `DraCode.Agent.Agents.Coding.*`, `DraCode.Agent.Agents.Media.*`
- **Parallel Execution** (v2.4.2): 4-8x speedup with parallelized Drake, Wyvern, and monitoring services
- **Drake Execution Service** (v2.4.1): New background service bridges Wyvern analysis to task execution
- **Wyvern Analysis Persistence** (v2.4.1): Analysis survives server restarts with disk persistence
- **Retry Analysis Tool** (v2.4.1): Retry failed Wyvern analysis via Warden agent
- **Performance Optimizations** (v2.4.1): JSON serialization caching, 64KB WebSocket buffer
- **Kobold Implementation Planner** (v2.4.0): Creates structured plans before task execution
- **Allowed External Paths** (v2.4.0): Per-project access control for external directories
- **LLM Retry Logic** (v2.4.0): Exponential backoff for all 10 providers
- **Dragon Council** (v2.4.0): Specialized sub-agents (Sage, Seeker, Sentinel, Warden)

---

## KoboldLair Multi-Agent System

KoboldLair is an autonomous hierarchical multi-agent system where **Dragon is your only interactive interface**. All other agents work automatically in the background.

### Agent Documentation

| Agent | Role | Interactive | Documentation |
|-------|------|-------------|---------------|
| **Dragon** | Requirements gathering | Yes | [Dragon-Requirements-Agent.md](Dragon-Requirements-Agent.md) |
| **Wyvern** | Project analysis & task organization | Automatic | [Wyvern-Project-Analyzer.md](Wyvern-Project-Analyzer.md) |
| **Wyrm** | Task delegation & agent selection | Automatic | Part of KoboldLair orchestration |
| **Drake** | Task supervision & Kobold management | Automatic | [Drake-Monitoring-System.md](Drake-Monitoring-System.md) |
| **Kobold Planner** | Implementation planning before execution | Automatic | [Kobold-System.md](Kobold-System.md) |
| **Kobold** | Code generation workers | Automatic | [Kobold-System.md](Kobold-System.md) |

### How It Works

1. **You interact with Dragon** (web chat) to describe project requirements
2. **Dragon creates specification** → automatically registers project
3. **Wyvern is assigned** → background service runs every 60s
4. **Wyvern analyzes** → creates organized task files
5. **Drake monitors** → assigns tasks to Kobolds (via Wyrm for agent selection)
6. **Kobold Planner** → creates implementation plan with atomic steps
7. **Kobolds execute plan** → generates code step-by-step (resumable)

Only Dragon requires interaction - everything else is automatic!

### Data Storage (Consolidated Per-Project Folders)

The projects path is configurable via `appsettings.json` under `KoboldLair.ProjectsPath` (default: `./projects`).

```
{ProjectsPath}/
    projects.json                     # Project registry
    {sanitized-project-name}/         # Per-project folder (e.g., my-todo-app/)
        specification.md              # Project specification
        specification.features.json   # Feature list
        analysis.md                   # Wyvern analysis report (human-readable)
        analysis.json                 # Wyvern analysis (machine-readable, persisted)
        tasks/                        # Task files subdirectory
            {area}-tasks.md           # Task files (e.g., backend-tasks.md)
        workspace/                    # Generated code output
```

---

## Documentation Structure

### Core Documentation
- **[Full Project Specification](FULL_PROJECT_SPECIFICATION.md)** - Complete specification to regenerate entire project
- **[Changelog](CHANGELOG.md)** - Version history and release notes
- **[Agent Options](AGENT_OPTIONS.md)** - Agent configuration options
- **[New Agent Types](NEW_AGENT_TYPES.md)** - PHP, Python, SVG, Bitmap, and Media agents

### Architecture
- [Architecture Specification](architecture/ARCHITECTURE_SPECIFICATION.md) - System architecture and design
- [Technical Specification](architecture/TECHNICAL_SPECIFICATION.md) - Comprehensive technical documentation
- [Tool Specifications](architecture/TOOL_SPECIFICATIONS.md) - Built-in tools documentation

### Setup Guides
- **[Provider Setup](setup-guides/PROVIDER_SETUP.md)** - All LLM providers (OpenAI, Claude, Gemini, Azure, Ollama)
- [CLI Options Guide](setup-guides/CLI_OPTIONS.md) - Complete command-line reference
- [WebSocket Quick Start](setup-guides/WEBSOCKET_QUICKSTART.md) - Getting started with the WebSocket system
- [Web Client Multi-Provider Guide](setup-guides/WEB_CLIENT_MULTI_PROVIDER_GUIDE.md) - Using multiple providers
- [GitHub OAuth Setup](setup-guides/GITHUB_OAUTH_SETUP.md) - GitHub Copilot OAuth configuration

### Troubleshooting
- **[Troubleshooting Guide](troubleshooting/TROUBLESHOOTING.md)** - All common issues and solutions

### Development
- [Implementation Plan](development/IMPLEMENTATION_PLAN.md) - Development roadmap and guidelines
- [Data and Mock Guidelines](DATA_AND_MOCK_GUIDELINES.md) - Data transparency guidelines

---

## Project-Specific Documentation

Each project has its own README:
- [DraCode.KoboldLair README](../DraCode.KoboldLair/README.md) - Multi-agent core library (agents, factories, orchestrators, services)
- [DraCode.KoboldLair.Server README](../DraCode.KoboldLair.Server/README.md) - Multi-agent backend
- [DraCode.KoboldLair.Client README](../DraCode.KoboldLair.Client/README.md) - Multi-agent web UI
- [DraCode.WebSocket README](../DraCode.WebSocket/README.md) - WebSocket API server
- [DraCode.Web README](../DraCode.Web/README.md) - Web client
- [DraCode.AppHost README](../DraCode.AppHost/README.md) - .NET Aspire orchestration
- [VS Code README](../.vscode/README.md) - VS Code configuration

---

## Contributing

When adding new documentation:
1. Place it in the appropriate subdirectory
2. Update this index file
3. Link to it from relevant documents
4. Use descriptive file names

For version history, update [CHANGELOG.md](CHANGELOG.md).
