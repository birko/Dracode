# DraCode Documentation

Welcome to the DraCode documentation. This directory contains all technical documentation, guides, and specifications.

📖 **[Full Project Specification](FULL_PROJECT_SPECIFICATION.md)** - Complete project spec for regeneration  
📝 **[Changelog](CHANGELOG.md)** - Version history and release notes  
🏰 **[KoboldLair Server README](../DraCode.KoboldLair.Server/README.md)** - Multi-agent autonomous coding system backend  
🎨 **[KoboldLair Client README](../DraCode.KoboldLair.Client/README.md)** - Multi-agent autonomous coding system web UI

## 🏰 KoboldLair Multi-Agent System

**NEW!** KoboldLair is an autonomous hierarchical multi-agent system where **Dragon is your only interactive interface**. All other agents work automatically in the background.

### Agent Documentation

| Agent | Role | Interactive | Documentation |
|-------|------|-------------|---------------|
| 🐉 **Dragon** | Requirements gathering | ✅ Yes | [Dragon-Requirements-Agent.md](Dragon-Requirements-Agent.md) |
| 🐲 **Wyvern** | Project analysis & task organization | ❌ Automatic | [Wyvern-Project-Analyzer.md](Wyvern-Project-Analyzer.md) |
| 🦅 **Drake** | Task supervision & Kobold management | ❌ Automatic | [Drake-Monitoring-System.md](Drake-Monitoring-System.md) |
| 👹 **Kobold** | Code generation workers | ❌ Automatic | [Kobold-System.md](Kobold-System.md), [Kobold-State-Management.md](Kobold-State-Management.md) |

### KoboldLair Resources
- **[KoboldLair Server README](../DraCode.KoboldLair.Server/README.md)** - Complete backend documentation
- **[KoboldLair Client README](../DraCode.KoboldLair.Client/README.md)** - Web UI documentation

### How It Works
1. **You interact with Dragon** (web chat) to describe project requirements
2. **Dragon creates specification** → automatically registers project
3. **Wyvern is assigned** → background service runs every 60s
4. **Wyvern analyzes** → creates organized task files
5. **Drake monitors** → assigns tasks to Kobolds
6. **Kobolds generate code** → completely automatic

Only Dragon requires interaction - everything else is automatic!

## Project Lifecycle

Projects in KoboldLair follow a complete lifecycle from creation through execution:

### Lifecycle Stages

```
1. USER CREATES PROJECT
   User: Dragon page → Create new project "MyApp"
   ↓
2. DRAGON GATHERS REQUIREMENTS
   User: Chats with Dragon about requirements
   Dragon: Asks clarifying questions
   ↓
3. SPECIFICATION CREATED
   Dragon: Creates MyApp_specification.md in ./specifications/
   ↓
4. PROJECT AUTO-REGISTERED
   DragonService: Calls ProjectService.RegisterProject()
   ProjectRepository: Saves to ./projects/projects.json
   ↓
5. Wyvern ASSIGNED (Background Service)
   WyvernProcessingService: Detects new project (every 60s)
   ProjectService: Assigns Wyvern
   Wyvern: Analyzes specification
   ↓
6. TASKS GENERATED
   Wyvern: Creates task breakdown
   Project: status → "Analyzed"
   Tasks: Saved as task files
   ↓
7. DRAKE SUPERVISES
   DrakeMonitoringService: Detects tasks (every 60s)
   Drake: Assigns tasks to Kobolds
   ↓
8. KOBOLDS EXECUTE
   Kobolds: Write actual code
   Output: Files in ./workspace/MyApp/
   ↓
9. PROJECT COMPLETE
   Project: status → "Complete"
   Code: Ready for review and deployment
```

### Data Storage

- **Projects**: `./projects/projects.json`
- **Specifications**: `./specifications/{project}_specification.md`
- **Tasks**: `./tasks/{project}/`
- **Output**: `./workspace/{project}/`

All data is real and persists to disk - no mock data.

## 📚 Documentation Structure

### 🎯 Core Documentation
- **[Full Project Specification](FULL_PROJECT_SPECIFICATION.md)** - Comprehensive specification to regenerate entire project from scratch
- **[Changelog](CHANGELOG.md)** - Complete version history and release notes

### 🏗️ Architecture
Technical specifications and architecture documentation:
- [Architecture Specification](architecture/ARCHITECTURE_SPECIFICATION.md) - System architecture and design
- [Technical Specification](architecture/TECHNICAL_SPECIFICATION.md) - Comprehensive technical documentation
- [Tool Specifications](architecture/TOOL_SPECIFICATIONS.md) - Built-in tools documentation
- **[New Agent Types](NEW_AGENT_TYPES.md)** - PHP, Python, SVG, Bitmap, and Media agents documentation

### 🚀 Setup Guides
Step-by-step guides for setting up and configuring DraCode:
- [CLI Options Guide](setup-guides/CLI_OPTIONS.md) - Complete command-line reference
- [WebSocket Quick Start](setup-guides/WEBSOCKET_QUICKSTART.md) - Getting started with the WebSocket system
- [Web Client Multi-Provider Guide](setup-guides/WEB_CLIENT_MULTI_PROVIDER_GUIDE.md) - Using multiple providers in web client

#### Provider Setup Guides
- [Azure OpenAI Setup](setup-guides/AZURE_OPENAI_SETUP.md) - Azure OpenAI Service configuration
- [Claude Setup](setup-guides/CLAUDE_SETUP.md) - Anthropic Claude configuration
- [Gemini Setup](setup-guides/GEMINI_SETUP.md) - Google Gemini configuration
- [GitHub OAuth Setup](setup-guides/GITHUB_OAUTH_SETUP.md) - GitHub Copilot OAuth configuration
- [Ollama Setup](setup-guides/OLLAMA_SETUP.md) - Local models with Ollama

### 🛠️ Development
Documentation for developers and contributors:
- [Implementation Plan](development/IMPLEMENTATION_PLAN.md) - Development roadmap and guidelines

### 🔧 Troubleshooting
Problem-solving guides and debugging help:
- [General Troubleshooting](troubleshooting/TROUBLESHOOTING.md) - Comprehensive debugging guide for web client
- [Provider Grid Troubleshooting](troubleshooting/PROVIDER_GRID_TROUBLESHOOTING.md) - WebSocket provider grid issues

### 📝 Version History
- [Changelog](CHANGELOG.md) - Complete version history and release notes

## 🎯 Quick Links

### For Users
- [Main README](../README.md) - Project overview and quick start
- [WebSocket Quick Start](setup-guides/WEBSOCKET_QUICKSTART.md) - Get started quickly
- [Changelog](CHANGELOG.md) - See what's new

### For Developers
- [Full Project Specification](FULL_PROJECT_SPECIFICATION.md) - Regenerate entire project
- [Architecture Specification](architecture/ARCHITECTURE_SPECIFICATION.md) - Understand the system
- [Implementation Plan](development/IMPLEMENTATION_PLAN.md) - Development guidelines

### For Troubleshooting
- [General Troubleshooting](troubleshooting/TROUBLESHOOTING.md) - Start here
- [Provider Grid Troubleshooting](troubleshooting/PROVIDER_GRID_TROUBLESHOOTING.md) - WebSocket-specific issues

## 📖 Project-Specific Documentation

Each project has its own README with specific details:
- [DraCode.KoboldLair.Server README](../DraCode.KoboldLair.Server/README.md) - Multi-agent autonomous coding system backend
- [DraCode.KoboldLair.Client README](../DraCode.KoboldLair.Client/README.md) - Multi-agent autonomous coding system web UI
- [DraCode.WebSocket README](../DraCode.WebSocket/README.md) - WebSocket API server documentation
- [DraCode.Web README](../DraCode.Web/README.md) - Web client documentation
- [DraCode.AppHost README](../DraCode.AppHost/README.md) - .NET Aspire orchestration documentation

## 🤝 Contributing

When adding new documentation:
1. Place it in the appropriate subdirectory
2. Update this index file
3. Link to it from relevant documents
4. Keep file names descriptive and in UPPER_CASE.md format for consistency

For version history and changes, update [CHANGELOG.md](CHANGELOG.md).

## 📧 Support

For issues, questions, or contributions, please open an issue on GitHub.
