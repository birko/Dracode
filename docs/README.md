# DraCode Documentation

Welcome to the DraCode documentation. This directory contains all technical documentation, guides, and specifications.

## Quick Links

- **[Full Project Specification](FULL_PROJECT_SPECIFICATION.md)** - Complete spec for regenerating the project
- **[Changelog](CHANGELOG.md)** - Version history and release notes (v2.6.0 - February 2026)
- **[KoboldLair Core Library](../DraCode.KoboldLair/README.md)** - Multi-agent orchestration library
- **[KoboldLair Server](../DraCode.KoboldLair.Server/README.md)** - Multi-agent backend
- **[KoboldLair Client](../DraCode.KoboldLair.Client/README.md)** - Multi-agent web UI

## Latest Updates (v2.6.0)

- **Specification Version Tracking**: Automatic detection of specification changes (NEW - 2026-02-26)
  - SHA-256 content hashing for change detection
  - Version history tracking with timestamps
  - Kobolds automatically reload updated specifications
  - Tasks track which spec version they were created for
  - Dragon tool: `view_specification_history` for audit trail
  - Prevents specification drift during active execution
- **Project Verification System**: Automatic validation after task completion (NEW - 2026-02-15)
- **Shared Planning Context Service**: Cross-agent coordination and learning from past executions
  - File conflict detection prevents parallel work on same files
  - Historical insights from similar tasks improve planning
  - Real-time agent tracking and activity monitoring
  - Thread-safe with LRU caching (max 50 projects)
  - Auto-persists to `planning-context.json` per project
- **Wyrm Pre-Analysis Workflow**: Two-phase analysis with initial recommendations
  - Wyrm analyzes specs for languages, tech stack, agent types, complexity
  - Creates `wyrm-recommendation.json` for Wyvern consumption
  - New `WyrmAssigned` status between `New` and `Analyzed`
  - Separates concerns: Wyrm = recommendations, Wyvern = detailed tasks
- **Agent Creation Pattern Consistency**: All factories now use consistent creation patterns
  - WyrmFactory fixed to use `KoboldLairAgentFactory.Create`
  - Audit verified compliance across 4 factories
  - Dragon Council sub-agents documented as acceptable exceptions
- **Network Error Handling Fix** (v2.5.1): Critical bug fixed where network errors incorrectly marked tasks as complete
- **OrchestratorAgent Base Class** (v2.5.0): New abstract base for Dragon, Wyrm, Wyvern with shared helper methods
- **Agent Reorganization** (v2.5.0): 23 agents organized into hierarchical folder structure
- **Parallel Execution** (v2.4.2): 4-8x speedup with parallelized Drake, Wyvern, and monitoring services
- **Kobold Implementation Planner** (v2.4.0): Creates structured plans before task execution
- **LLM Retry Logic** (v2.4.0): Exponential backoff for all 10 providers
- **Dragon Council** (v2.4.0): Specialized sub-agents (Sage, Seeker, Sentinel, Warden)

---

## KoboldLair Multi-Agent System

KoboldLair is an autonomous hierarchical multi-agent system where **Dragon is your only interactive interface**. All other agents work automatically in the background.

### Agent Documentation

| Agent | Role | Interactive | Documentation |
|-------|------|-------------|---------------|
| **Dragon** | Requirements gathering | Yes | [Dragon-Requirements-Agent.md](Dragon-Requirements-Agent.md) |
| **Wyrm** | Pre-analysis & recommendations | Automatic | Part of KoboldLair orchestration |
| **Wyvern** | Project analysis & task organization | Automatic | [Wyvern-Project-Analyzer.md](Wyvern-Project-Analyzer.md) |
| **Drake** | Task supervision & Kobold management | Automatic | [Drake-Monitoring-System.md](Drake-Monitoring-System.md) |
| **Kobold Planner** | Implementation planning before execution | Automatic | [Kobold-Planner-Agent.md](Kobold-Planner-Agent.md) |
| **Kobold** | Code generation workers | Automatic | [Kobold-System.md](Kobold-System.md) |
| **Verification** | Validation after completion | Automatic | [Verification-System.md](Verification-System.md) |

### How It Works

1. **You interact with Dragon** (web chat) to describe project requirements
2. **Dragon creates specification** → automatically registers project
3. **Wyrm is assigned** → pre-analyzes specification for languages, tech stack, agent recommendations
4. **Wyvern is assigned** → background service runs every 60s
5. **Wyvern analyzes** → creates organized task files using Wyrm's recommendations
6. **Drake monitors** → assigns tasks to Kobolds (via Wyrm for final agent selection)
7. **Kobold Planner** → creates implementation plan with atomic steps
8. **Kobolds execute plan** → generates code step-by-step (resumable, with shared context coordination)
9. **Verification runs** → validates build, tests, linting after all tasks complete
10. **Fix tasks created** → if verification fails, Drake assigns fixes automatically

Only Dragon requires interaction - everything else is automatic!

### Data Storage (Consolidated Per-Project Folders)

The projects path is configurable via `appsettings.json` under `KoboldLair.ProjectsPath` (default: `./projects`).

```
{ProjectsPath}/
    projects.json                     # Project registry
    {sanitized-project-name}/         # Per-project folder (e.g., my-todo-app/)
        specification.md              # Project specification
        specification.features.json   # Features + version metadata (wrapped format)
        wyrm-recommendation.json      # Wyrm pre-analysis (v2.6.0)
        analysis.md                   # Wyvern analysis report (human-readable)
        analysis.json                 # Wyvern analysis (machine-readable, persisted)
        tasks/                        # Task files subdirectory
            {area}-tasks.md           # Task files (e.g., backend-tasks.md)
        workspace/                    # Generated code output
        kobold-plans/                 # Implementation plans
            {plan-filename}-plan.json # Machine-readable plan
            {plan-filename}-plan.md   # Human-readable plan
            plan-index.json           # Plan lookup index
        planning-context.json         # Shared planning context (v2.6.0)
```

---

## Documentation Structure

### Core Documentation
- **[Full Project Specification](FULL_PROJECT_SPECIFICATION.md)** - Complete specification to regenerate entire project
- **[Changelog](CHANGELOG.md)** - Version history and release notes
- **[Agent Options](AGENT_OPTIONS.md)** - Agent configuration options
- **[New Agent Types](NEW_AGENT_TYPES.md)** - PHP, Python, SVG, Bitmap, and Media agents
- **[Plan Status Tracking](Plan-Status-Tracking.md)** - Implementation plan status management
- **[Shared Planning Context Service](SharedPlanningContextService.md)** - Cross-agent coordination and learning (NEW - 2026-02-09)
- **[Verification System](Verification-System.md)** - Automatic project validation system (NEW - 2026-02-15)
- **[Background Services](Background-Services.md)** - Overview of all background services and their intervals

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
