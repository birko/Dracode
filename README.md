# DraCode

**DraCode** is an AI-powered coding agent CLI that leverages Large Language Models (LLMs) to autonomously perform coding tasks within a sandboxed workspace. It supports multiple LLM providers and provides tools for file manipulation, code search, and command execution.

## 🚀 Features

- **Multi-Provider LLM Support**: OpenAI, Claude, Gemini, Azure OpenAI, Ollama, GitHub Copilot
- **Autonomous Agent System**: Multi-turn conversations with iterative problem solving
- **Tool System**: 5 built-in tools for code manipulation
  - `list_files` - Directory listing with recursive search
  - `read_file` - Read file contents
  - `write_file` - Create/modify files
  - `search_code` - Grep-like code search with regex support
  - `run_command` - Execute shell commands with timeout
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

| Provider | Configuration | Models |
|----------|--------------|--------|
| **OpenAI** | `OPENAI_API_KEY` | gpt-4o, gpt-4, gpt-3.5-turbo |
| **Claude** | `ANTHROPIC_API_KEY` | claude-3-5-sonnet, claude-3-5-haiku |
| **Gemini** | `GEMINI_API_KEY` | gemini-2.0-flash-exp |
| **Azure OpenAI** | `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY` | Custom deployments |
| **Ollama** | None (local) | llama3.2, mistral, etc. |
| **GitHub Copilot** | `GITHUB_CLIENT_ID` (OAuth) | gpt-4o, gpt-4-turbo |

## 🎯 Usage

### Basic Usage

```bash
dotnet run --project DraCode -- --provider=openai --task="Create a hello world C# program"
```

### Interactive Mode

```bash
dotnet run --project DraCode
# You'll be prompted to enter a task
```

### Task from File

```bash
dotnet run --project DraCode -- --task="path/to/task.txt"
```

### Examples

```bash
# Refactor code
dotnet run -- --provider=claude --task="Refactor Program.cs to use dependency injection"

# Run tests
dotnet run -- --provider=gemini --task="Run all unit tests and fix any failures"

# Generate documentation
dotnet run -- --task="Generate API documentation for all public methods"
```

## 🔐 GitHub Copilot OAuth Setup

For GitHub Copilot provider, see [GITHUB_OAUTH_SETUP.md](DraCode/GITHUB_OAUTH_SETUP.md) for detailed OAuth configuration instructions.

Quick setup:
1. Create GitHub OAuth App at https://github.com/settings/developers
2. Enable Device Flow
3. Set `GITHUB_CLIENT_ID` environment variable
4. Run with `--provider=githubcopilot`

## 📚 Documentation

- **[Technical Specification](TECHNICAL_SPECIFICATION.md)** - Comprehensive technical documentation
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
