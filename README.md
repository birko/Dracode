# DraCode

**DraCode** is an AI-powered coding agent CLI that leverages Large Language Models (LLMs) to autonomously perform coding tasks within a sandboxed workspace. It supports multiple LLM providers and provides tools for file manipulation, code search, and command execution.

## 🌟 NEW: Multi-Agent WebSocket System

DraCode now includes a **WebSocket server and modern TypeScript web client** that allows you to:
- 🔄 **Connect to multiple LLM providers simultaneously** through a single WebSocket connection
- 📊 **Compare responses** from different providers (OpenAI, Claude, Gemini, etc.) side-by-side
- 🏃 **Run multiple agents in parallel**, each with independent conversation history
- 🎯 **Switch between providers** using a tabbed interface
- 🔐 **Secure configuration** with server-side API key management
- 🛡️ **Token-based authentication** with optional IP address binding to prevent token misuse
- 💎 **Modern tech stack**: TypeScript, ES modules, Flexbox CSS (zero dependencies!)

**Quick Start:**
```bash
dotnet run --project DraCode.AppHost
# Open http://localhost:5001 in your browser
```

📖 **Learn More**: [WebSocket Quick Start](docs/setup-guides/WEBSOCKET_QUICKSTART.md) | [Changelog](docs/CHANGELOG.md) (v2.4.1)

## 🏰 KoboldLair - Autonomous Multi-Agent Coding System

**KoboldLair** is an intelligent, hierarchical multi-agent system that autonomously transforms your ideas into working code:

🐉 **Dragon** (Interactive) - Your only touchpoint. Conduct conversational requirements gathering, refine specifications.
🐲 **Wyrm** (Automatic) - Analyzes specifications, breaks down into organized tasks, manages dependencies.
🦅 **Drake** (Automatic) - Supervises task execution, monitors progress, handles errors.
📋 **Kobold Planner** (Automatic) - Creates implementation plans with atomic steps before code generation.
👹 **Kobold** (Automatic) - Executes plans step-by-step, writing the actual code.

**Key Features:**
- 💬 **Interactive Dragon Chat** - Natural conversation interface for requirements
- 📋 **Implementation Planning** - Kobold Planner creates structured plans before execution (resumable)
- 🔄 **Automated Workflow** - Wyverns, Drakes, and Kobolds work automatically in background
- 📊 **Real-time Visualization** - Animated hierarchy display showing agent relationships and status
- 📁 **Project Management** - Automatic project tracking with metadata and output locations
- ⏱️ **Background Processing** - Services run every 60 seconds checking for new work
- 🎨 **Modern UI** - Three-page interface: Status Monitor, Dragon Chat, Hierarchy View
- 🔀 **Git Integration** - Branch management, merge operations, conflict detection
- 💭 **Thinking Indicator** - Real-time processing feedback during Dragon chat
- 🔒 **External Path Access** - Per-project access control for directories outside workspace
- 🔄 **LLM Retry Logic** - Robust API handling with exponential backoff for all providers

**Quick Start:**
```bash
dotnet run --project DraCode.AppHost
# Open KoboldLair from the Aspire dashboard
```

📖 **Learn More**: [KoboldLair Core Library](DraCode.KoboldLair/README.md) | [KoboldLair Server](DraCode.KoboldLair.Server/README.md) | [KoboldLair Client](DraCode.KoboldLair.Client/README.md)

## 🚀 Features

- **Multi-Provider LLM Support**: OpenAI, Claude, Gemini, Azure OpenAI, Ollama, GitHub Copilot, Z.AI, vLLM, SGLang, LlamaCpp
- **17 Specialized Agent Types**: Coding (C#, C++, JavaScript, TypeScript, PHP, Python, etc.), Web (HTML, CSS, React, Angular), Media (SVG, Bitmap, Image), and Diagramming
- **Multi-Task Execution**: Define and execute multiple tasks sequentially with fresh agent instances
- **Interactive CLI UI**: Beautiful Spectre.Console interface with provider selection menus
- **Verbose Mode Control**: Toggle between detailed execution info or clean minimal output
- **Autonomous Agent System**: Multi-turn conversations with iterative problem solving
- **Tool System**: 7 built-in tools + 10 Dragon-specific tools + 1 Planner tool
  - **Built-in**: `list_files`, `read_file`, `write_file`, `search_code`, `run_command`, `ask_user`, `display_text`
  - **Dragon Tools**: `git_status`, `git_merge`, `manage_specification`, `manage_features`, `approve_specification`, `list_projects`, `add_existing_project`, `select_agent`, `manage_external_paths`, `retry_analysis`
  - **Planner Tool**: `create_implementation_plan`
- **GitHub Copilot OAuth**: Integrated device flow authentication
- **Sandboxed Workspace**: All operations restricted to working directory
- **Flexible Configuration**: JSON config with environment variable overrides

## 📋 Requirements

- .NET 10.0 SDK or later
- API keys for your chosen LLM provider(s)

## 🔧 Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/DraCode.git
cd DraCode
```

2. Build the project:
```bash
dotnet build
```

3. Configure your API keys:
```bash
cp DraCode/appsettings.json DraCode/appsettings.local.json
# Edit appsettings.local.json with your API keys
```

## ⚙️ Configuration

### Quick Start

Edit `DraCode/appsettings.local.json`:

```json
{
  "Agent": {
    "Provider": "openai",
    "WorkingDirectory": "./workspace",
    "Verbose": true,
    "Tasks": [],
    "Providers": {
      "openai": {
        "apiKey": "sk-your-api-key-here",
        "model": "gpt-4o"
      }
    }
  }
}
```

### Environment Variables

Alternatively, set environment variables:

```bash
# Windows PowerShell
$env:OPENAI_API_KEY = "sk-your-api-key"

# Linux/Mac
export OPENAI_API_KEY="sk-your-api-key"
```

### Supported Providers

| Provider | Configuration | Models | Setup Guide |
|----------|--------------|--------|-------------|
| **OpenAI** | `OPENAI_API_KEY` | gpt-4o, gpt-4, gpt-3.5-turbo | - |
| **Claude** | `ANTHROPIC_API_KEY` | claude-3-5-sonnet, claude-3-5-haiku | [Setup Guide](docs/setup-guides/CLAUDE_SETUP.md) |
| **Gemini** | `GEMINI_API_KEY` | gemini-2.0-flash-exp | [Setup Guide](docs/setup-guides/GEMINI_SETUP.md) |
| **Azure OpenAI** | `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY` | Custom deployments | [Setup Guide](docs/setup-guides/AZURE_OPENAI_SETUP.md) |
| **Ollama** | None (local) | llama3.2, mistral, codellama | [Setup Guide](docs/setup-guides/OLLAMA_SETUP.md) |
| **GitHub Copilot** | `GITHUB_CLIENT_ID` (OAuth) | gpt-4o, gpt-4-turbo | [Setup Guide](docs/setup-guides/GITHUB_OAUTH_SETUP.md) |
| **Z.AI** | `ZHIPU_API_KEY` | glm-4.5-flash, glm-4.6-flash, glm-4.7 | [Setup Guide](docs/setup-guides/PROVIDER_SETUP.md) |
| **vLLM** | `VLLM_BASE_URL` (local) | Any supported model | [Setup Guide](docs/setup-guides/PROVIDER_SETUP.md) |
| **SGLang** | `SGLANG_BASE_URL` (local) | Any supported model | [Setup Guide](docs/setup-guides/PROVIDER_SETUP.md) |
| **LlamaCpp** | `LLAMACPP_BASE_URL` (local) | GGUF models | [Setup Guide](docs/setup-guides/PROVIDER_SETUP.md) |

## 🎯 Usage

### .NET Aspire (Recommended)

Run both services with a single command using .NET Aspire:

```bash
dotnet run --project DraCode.AppHost
```

This will:
- Start the WebSocket API server on port 5000
- Start the Web Client on port 5001  
- Launch the Aspire Dashboard for monitoring
- Enable service discovery and telemetry

See [DraCode.AppHost/README.md](DraCode.AppHost/README.md) for details.

### Manual Startup

Alternatively, run services separately:

**Terminal 1 - Start WebSocket API:**
```bash
dotnet run --project DraCode.WebSocket
```

**Terminal 2 - Start Web Client:**
```bash
dotnet run --project DraCode.Web
```

**Open browser:** `http://localhost:5001`

- **WebSocket API**: `ws://localhost:5000/ws` (DraCode.WebSocket)
- **Web Client**: `http://localhost:5001` (DraCode.Web)

See [DraCode.WebSocket/README.md](DraCode.WebSocket/README.md) for WebSocket API documentation and [DraCode.Web/README.md](DraCode.Web/README.md) for web client usage.

### Basic Usage

```bash
# Single task
dotnet run --project DraCode -- --provider=openai --task="Create a hello world C# program"

# Multiple tasks (comma-separated)
dotnet run --project DraCode -- --provider=openai --task="Create main.cs,Add logging,Run tests"
```

### Interactive Mode

```bash
dotnet run --project DraCode
# Interactive menus will guide you through:
# 1. Provider selection (if multiple configured)
# 2. Verbose output preference
# 3. Multi-task input (one per line, empty line to finish)
```

### Task from File

```bash
dotnet run --project DraCode -- --task="path/to/task.txt"
```

### Multi-Task Configuration

You can define multiple tasks in `appsettings.json`:

```json
{
  "Agent": {
    "Tasks": [
      "Create project structure",
      "Implement core functionality",
      "Add unit tests",
      "Generate documentation"
    ]
  }
}
```

### Verbose Output Control

```bash
# Enable detailed execution info (shows iterations, tool calls, stop reasons)
dotnet run -- --verbose --task="Your task"

# Disable verbose output (clean, minimal output)
dotnet run -- --quiet --task="Your task"
dotnet run -- --no-verbose --task="Your task"

# If omitted, interactive prompt will ask your preference
dotnet run -- --task="Your task"
```

### Examples

```bash
# Multiple tasks with detailed output
dotnet run -- --provider=claude --verbose --task="Create utils.cs,Add tests,Run build"

# Batch processing quietly
dotnet run -- --provider=gemini --quiet --task="Fix bug in auth.cs,Update docs,Commit changes"

# Interactive multi-task workflow
dotnet run -- --task="Generate API documentation for all public methods"
```

## 🔐 Provider Setup Guides

### Claude (Anthropic)
See [CLAUDE_SETUP.md](docs/setup-guides/CLAUDE_SETUP.md) for detailed setup instructions.

**Quick setup:**
1. Get API key from https://console.anthropic.com/
2. Set `ANTHROPIC_API_KEY` environment variable
3. Run with `--provider=claude`

### Google Gemini
See [GEMINI_SETUP.md](docs/setup-guides/GEMINI_SETUP.md) for detailed setup instructions.

**Quick setup:**
1. Get API key from https://makersuite.google.com/app/apikey
2. Set `GEMINI_API_KEY` environment variable
3. Run with `--provider=gemini`

### Azure OpenAI
See [AZURE_OPENAI_SETUP.md](docs/setup-guides/AZURE_OPENAI_SETUP.md) for detailed setup instructions.

**Quick setup:**
1. Create Azure OpenAI resource in Azure Portal
2. Deploy a model (e.g., gpt-4o)
3. Set `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_API_KEY` environment variables
4. Run with `--provider=azureopenai`

### Ollama (Local Models)
See [OLLAMA_SETUP.md](docs/setup-guides/OLLAMA_SETUP.md) for detailed setup instructions.

**Quick setup:**
1. Install Ollama from https://ollama.com/
2. Download a model: `ollama pull llama3.2`
3. Run with `--provider=ollama` (no API key needed!)

### GitHub Copilot
See [GITHUB_OAUTH_SETUP.md](docs/setup-guides/GITHUB_OAUTH_SETUP.md) for detailed OAuth configuration instructions.

**Quick setup:**
1. Create GitHub OAuth App at https://github.com/settings/developers
2. Enable Device Flow
3. Set `GITHUB_CLIENT_ID` environment variable
4. Run with `--provider=githubcopilot`

## 📚 Documentation

For complete documentation, see the [docs](docs/) directory.

### Quick Links
- **[Documentation Index](docs/README.md)** - Complete documentation overview
- **[Changelog](docs/CHANGELOG.md)** - Version history and release notes
- **[CLI Options Guide](docs/setup-guides/CLI_OPTIONS.md)** - Complete command-line reference
- **[WebSocket Quick Start](docs/setup-guides/WEBSOCKET_QUICKSTART.md)** - Get started with multi-agent system
- **[Architecture Specification](docs/architecture/ARCHITECTURE_SPECIFICATION.md)** - System architecture and design
- **[Technical Specification](docs/architecture/TECHNICAL_SPECIFICATION.md)** - Comprehensive technical documentation
- **[KoboldLair Core Library](DraCode.KoboldLair/README.md)** - Multi-agent orchestration library
- **[KoboldLair Server](DraCode.KoboldLair.Server/README.md)** - Multi-agent system backend
- **[KoboldLair Client](DraCode.KoboldLair.Client/README.md)** - Multi-agent system web UI

### Setup Guides
- **[Claude Setup](docs/setup-guides/CLAUDE_SETUP.md)** - Anthropic Claude configuration
- **[Gemini Setup](docs/setup-guides/GEMINI_SETUP.md)** - Google Gemini configuration
- **[Azure OpenAI Setup](docs/setup-guides/AZURE_OPENAI_SETUP.md)** - Azure OpenAI Service configuration
- **[Ollama Setup](docs/setup-guides/OLLAMA_SETUP.md)** - Local models with Ollama
- **[GitHub Copilot Setup](docs/setup-guides/GITHUB_OAUTH_SETUP.md)** - OAuth configuration

### Troubleshooting
- **[General Troubleshooting](docs/troubleshooting/TROUBLESHOOTING.md)** - Common issues and solutions
- **[Provider Grid Issues](docs/troubleshooting/PROVIDER_GRID_TROUBLESHOOTING.md)** - WebSocket-specific problems

## 🏗️ Architecture

```
┌─────────────────────────────────────────┐
│         DraCode.AppHost                 │  ← Aspire Orchestrator
└──────────────┬──────────────────────────┘
               │
    ┌──────────┴──────────┬───────────────────────┐
    │                     │                       │
    ▼                     ▼                       ▼
┌─────────────┐   ┌─────────────┐   ┌─────────────────────────┐
│ WebSocket   │   │   Web       │   │   KoboldLair Server     │
│ API         │◄──│   Client    │   │   (Multi-Agent System)  │
└─────────────┘   └─────────────┘   └───────┬─────────────────┘
                                            │
                                            ▼
                                    ┌─────────────────────┐
                                    │ KoboldLair Client   │
                                    │ (Web UI)            │
                                    └─────────────────────┘
```

## 🛠️ Development

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Project Structure

```
DraCode/
├── DraCode/                      # Main CLI application
├── DraCode.Agent/                # Agent library (17 agent types)
│   ├── Agents/                  # Agent implementations (Coding, Media, Diagramming)
│   ├── Auth/                    # OAuth implementation
│   ├── LLMs/                    # 10 LLM provider implementations
│   ├── Tools/                   # 7 built-in tools
│   └── Helpers/                 # Utility classes
├── DraCode.KoboldLair/           # Multi-agent core library
│   ├── Agents/                  # Dragon, Wyrm, Wyvern, KoboldPlanner agents
│   │   ├── SubAgents/          # Dragon sub-agents (Warden, Librarian, Architect)
│   │   └── Tools/              # Dragon & Planner tools (Git, Spec, Features, ExternalPaths)
│   ├── Factories/               # KoboldFactory, DrakeFactory, WyvernFactory
│   ├── Models/                  # Data models (Agents, Config, Projects, Tasks)
│   ├── Orchestrators/           # Drake, Wyvern, WyrmRunner
│   └── Services/                # GitService, ProjectService, ProviderConfigurationService
├── DraCode.KoboldLair.Server/    # Multi-agent WebSocket server
│   ├── Services/                # DragonService, DrakeMonitoringService, WyvernProcessingService
│   └── Models/                  # WebSocket message models
├── DraCode.KoboldLair.Client/    # KoboldLair Web UI
│   └── wwwroot/                 # Web UI (Status, Dragon Chat, Hierarchy)
├── DraCode.WebSocket/            # WebSocket API server
│   ├── Models/                  # WebSocket message models
│   └── Services/                # Agent connection manager
├── DraCode.Web/                  # Web client UI
│   └── wwwroot/                 # Static web assets
├── DraCode.AppHost/              # .NET Aspire orchestration
└── DraCode.ServiceDefaults/      # Shared Aspire configuration
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with .NET 10.0
- Inspired by AI coding assistants and autonomous agents
- Supports multiple LLM providers for flexibility

## ⚠️ Security Notes

- Never commit API keys or tokens to version control
- Use `appsettings.local.json` (gitignored) for sensitive data
- OAuth tokens stored in `~/.dracode/` (gitignored)
- All file operations sandboxed to working directory
- **WebSocket Authentication**: 
  - Optional token-based authentication available for WebSocket connections
  - Support for IP address binding to prevent token misuse
  - See [WebSocket README](DraCode.WebSocket/README.md) for configuration
  - Disabled by default for development convenience

## 📧 Support

For issues, questions, or contributions, please open an issue on GitHub.

---

**Note**: This project is in active development. Features and APIs may change.
