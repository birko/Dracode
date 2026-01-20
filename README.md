# DraCode

**DraCode** is an AI-powered coding agent CLI that leverages Large Language Models (LLMs) to autonomously perform coding tasks within a sandboxed workspace. It supports multiple LLM providers and provides tools for file manipulation, code search, and command execution.

## 🚀 Features

- **Multi-Provider LLM Support**: OpenAI, Claude, Gemini, Azure OpenAI, Ollama, GitHub Copilot
- **Multi-Task Execution**: Define and execute multiple tasks sequentially with fresh agent instances
- **Interactive CLI UI**: Beautiful Spectre.Console interface with provider selection menus
- **Verbose Mode Control**: Toggle between detailed execution info or clean minimal output
- **Autonomous Agent System**: Multi-turn conversations with iterative problem solving
- **Tool System**: 7 built-in tools for code manipulation
  - `list_files` - Directory listing with recursive search
  - `read_file` - Read file contents
  - `write_file` - Create/modify files
  - `search_code` - Grep-like code search with regex support
  - `run_command` - Execute shell commands with timeout
  - `ask_user` - Interactive user prompts
  - `display_text` - Formatted text output
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
| **Claude** | `ANTHROPIC_API_KEY` | claude-3-5-sonnet, claude-3-5-haiku | [Setup Guide](DraCode/CLAUDE_SETUP.md) |
| **Gemini** | `GEMINI_API_KEY` | gemini-2.0-flash-exp | [Setup Guide](DraCode/GEMINI_SETUP.md) |
| **Azure OpenAI** | `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY` | Custom deployments | [Setup Guide](DraCode/AZURE_OPENAI_SETUP.md) |
| **Ollama** | None (local) | llama3.2, mistral, codellama | [Setup Guide](DraCode/OLLAMA_SETUP.md) |
| **GitHub Copilot** | `GITHUB_CLIENT_ID` (OAuth) | gpt-4o, gpt-4-turbo | [Setup Guide](DraCode/GITHUB_OAUTH_SETUP.md) |

## 🎯 Usage

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
See [CLAUDE_SETUP.md](DraCode/CLAUDE_SETUP.md) for detailed setup instructions.

**Quick setup:**
1. Get API key from https://console.anthropic.com/
2. Set `ANTHROPIC_API_KEY` environment variable
3. Run with `--provider=claude`

### Google Gemini
See [GEMINI_SETUP.md](DraCode/GEMINI_SETUP.md) for detailed setup instructions.

**Quick setup:**
1. Get API key from https://makersuite.google.com/app/apikey
2. Set `GEMINI_API_KEY` environment variable
3. Run with `--provider=gemini`

### Azure OpenAI
See [AZURE_OPENAI_SETUP.md](DraCode/AZURE_OPENAI_SETUP.md) for detailed setup instructions.

**Quick setup:**
1. Create Azure OpenAI resource in Azure Portal
2. Deploy a model (e.g., gpt-4o)
3. Set `AZURE_OPENAI_ENDPOINT` and `AZURE_OPENAI_API_KEY` environment variables
4. Run with `--provider=azureopenai`

### Ollama (Local Models)
See [OLLAMA_SETUP.md](DraCode/OLLAMA_SETUP.md) for detailed setup instructions.

**Quick setup:**
1. Install Ollama from https://ollama.com/
2. Download a model: `ollama pull llama3.2`
3. Run with `--provider=ollama` (no API key needed!)

### GitHub Copilot
See [GITHUB_OAUTH_SETUP.md](DraCode/GITHUB_OAUTH_SETUP.md) for detailed OAuth configuration instructions.

**Quick setup:**
1. Create GitHub OAuth App at https://github.com/settings/developers
2. Enable Device Flow
3. Set `GITHUB_CLIENT_ID` environment variable
4. Run with `--provider=githubcopilot`

## 📚 Documentation

- **[CLI Options Guide](DraCode/CLI_OPTIONS.md)** - Complete command-line reference
- **[Claude Setup Guide](DraCode/CLAUDE_SETUP.md)** - Anthropic Claude configuration
- **[Gemini Setup Guide](DraCode/GEMINI_SETUP.md)** - Google Gemini configuration
- **[Azure OpenAI Setup](DraCode/AZURE_OPENAI_SETUP.md)** - Azure OpenAI Service configuration
- **[Ollama Setup Guide](DraCode/OLLAMA_SETUP.md)** - Local models with Ollama
- **[GitHub Copilot Setup](DraCode/GITHUB_OAUTH_SETUP.md)** - OAuth configuration
- **[Technical Specification](TECHNICAL_SPECIFICATION.md)** - Comprehensive technical documentation
- **[Architecture Specification](ARCHITECTURE_SPECIFICATION.md)** - System architecture and design
- **[Implementation Plan](IMPLEMENTATION_PLAN.md)** - Development roadmap and guidelines
- **[Tool Specifications](TOOL_SPECIFICATIONS.md)** - Built-in tools documentation
- **[GitHub OAuth Setup](DraCode/GITHUB_OAUTH_SETUP.md)** - OAuth configuration guide

## 🏗️ Architecture

```
┌─────────────────────────────────────────┐
│           DraCode CLI                   │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│          Agent Core                     │
│  ┌──────────┐  ┌──────┐  ┌───────────┐ │
│  │  Tools   │  │ LLM  │  │  OAuth    │ │
│  │  System  │  │ API  │  │  Service  │ │
│  └──────────┘  └──────┘  └───────────┘ │
└─────────────────────────────────────────┘
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
├── DraCode/              # Main CLI application
├── DraCode.Agent/        # Agent library
│   ├── Auth/            # OAuth implementation
│   ├── LLMs/            # LLM provider implementations
│   ├── Tools/           # Tool system
│   └── Helpers/         # Utility classes
└── TECHNICAL_SPECIFICATION.md
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

## 📧 Support

For issues, questions, or contributions, please open an issue on GitHub.

---

**Note**: This project is in active development. Features and APIs may change.
