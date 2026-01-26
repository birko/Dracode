# 🏰 KoboldTown - AI-Powered Automated Development Workflow

**KoboldTown** is an intelligent, automated AI agent system that transforms your project ideas into working code. Simply describe what you want to build, and watch as a hierarchy of specialized AI agents automatically analyzes, plans, and executes your project.

## 🌟 What Makes KoboldTown Special?

**One Interactive Interface, Fully Automated Workflow**

Unlike other AI coding tools, KoboldTown separates *what you want* from *how it gets built*:

- 🎯 **You talk to Dragon** - A friendly AI that gathers your requirements through natural conversation
- 🤖 **Everything else is automatic** - Specialized agents handle analysis, planning, and execution
- 📊 **You monitor progress** - Watch the workflow in real-time through beautiful visualizations
- ✅ **You get results** - Organized, working code delivered to your workspace

## 🚀 Quick Start

### Prerequisites

- .NET 10.0 SDK or later
- An API key for your preferred LLM provider (OpenAI, Claude, Gemini, Azure OpenAI, or Ollama)

### Installation & Setup

1. **Clone and build:**
```bash
git clone https://github.com/yourusername/DraCode.git
cd DraCode
dotnet build
```

2. **Configure your API key** (choose one method):

**Option A:** Create `DraCode.KoboldTown/appsettings.local.json`:
```json
{
  "Orchestrator": {
    "Provider": "openai",
    "ApiKey": "your-api-key-here",
    "Model": "gpt-4o"
  }
}
```

**Option B:** Set environment variable:
```bash
# Windows
$env:OPENAI_API_KEY = "your-api-key"

# Linux/Mac
export OPENAI_API_KEY="your-api-key"
```

3. **Run KoboldTown:**
```bash
dotnet run --project DraCode.KoboldTown
```

4. **Open your browser:**
```
http://localhost:5000/dragon.html
```

🎉 That's it! You're ready to start building projects with Dragon!

## 🎭 Meet the KoboldTown Team

KoboldTown uses a hierarchy of specialized AI agents. **Only Dragon is interactive** - everything else runs automatically!

### 🐉 Dragon - Requirements Gatherer
**⭐ THE ONLY INTERACTIVE INTERFACE ⭐**

Dragon is your friendly project consultant who:
- Chats with you naturally to understand your goals
- Asks clarifying questions about requirements
- Gathers technical specifications
- Creates detailed specification documents

**Where:** `/dragon.html` - **Start here!**

---

### 🐲 Wyrm - Specification Analyzer
**✨ FULLY AUTOMATIC ✨**

Wyrm automatically:
- Monitors for new specifications (every 60 seconds)
- Analyzes requirements and architecture
- Breaks projects into logical work areas
- Creates organized, dependency-aware task lists
- Generates task markdown files

**No user interaction needed!**

---

### 🦎 Drake - Task Supervisor
**✨ FULLY AUTOMATIC ✨**

Drake supervisors automatically:
- Monitor task queues (every 60 seconds)
- Create supervisor instances per project
- Assign tasks to available Kobolds
- Track progress and dependencies
- Update task statuses

**No user interaction needed!**

---

### ⚙️ Kobold - Code Executor
**✨ FULLY AUTOMATIC ✨**

Kobold workers automatically:
- Execute assigned coding tasks
- Generate actual code files
- Run tests and validations
- Report completion status
- Handle errors and retries

**Available Kobold Specializations:**
- **Coding**: `csharp`, `cpp`, `assembler`, `javascript`, `typescript`
- **Web**: `css`, `html`, `react`, `angular`
- **Backend**: `php`, `python`
- **Media**: `svg`, `bitmap`, `image`, `media`
- **Visualization**: `diagramming`

Each Kobold is an expert in their domain with deep knowledge of best practices, frameworks, and tools!

**No user interaction needed!**

## 🔄 How It Works

```
┌─────────────────────────────────────────────────────────────┐
│  STEP 1: DESCRIBE YOUR PROJECT (INTERACTIVE)                │
│  👤 You → 🐉 Dragon                                         │
│                                                              │
│  "Create a REST API for managing customer orders with       │
│   authentication, CRUD operations, and email notifications" │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  STEP 2: SPECIFICATION CREATED (AUTOMATIC)                  │
│  🐉 Dragon → 📄 Saves specification.md                      │
│                                                              │
│  File: ./specifications/customer-orders-api.md              │
│  Contains: Requirements, architecture, success criteria     │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  STEP 3: ANALYSIS (AUTOMATIC - every 60s)                  │
│  🐲 Wyrm Processing Service                                │
│                                                              │
│  ✓ Detects new specification                               │
│  ✓ Analyzes requirements                                   │
│  ✓ Identifies work areas                                   │
│  ✓ Creates task lists                                      │
│                                                              │
│  Output: ./workspace/customer-orders-api/*-tasks.md         │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  STEP 4: TASK ASSIGNMENT (AUTOMATIC - every 60s)           │
│  🦎 Drake Monitoring Service                               │
│                                                              │
│  ✓ Detects new tasks                                       │
│  ✓ Creates Drake supervisors                              │
│  ✓ Assigns to Kobold workers                              │
│  ✓ Manages dependencies                                   │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  STEP 5: CODE GENERATION (AUTOMATIC)                       │
│  ⚙️ Kobold Workers                                         │
│                                                              │
│  ✓ Execute tasks                                           │
│  ✓ Generate code files                                    │
│  ✓ Create tests & docs                                    │
│  ✓ Report completion                                      │
└──────────────────────────┬───────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│  STEP 6: YOUR CODE IS READY! 🎉                            │
│  📦 ./workspace/customer-orders-api/                        │
│     ├── src/ (application code)                            │
│     ├── tests/ (unit tests)                                │
│     ├── docs/ (documentation)                              │
│     └── README.md                                          │
└─────────────────────────────────────────────────────────────┘
```

## 🖥️ The Three Pages

### 1. 🐉 Dragon Chat - `/dragon.html`
**⭐ START HERE - INTERACTIVE ⭐**

Your only point of interaction. Chat with Dragon to:
- Describe what you want to build
- Answer clarifying questions
- Provide technical requirements
- Approve the specification

### 2. 📊 Status Monitor - `/` or `/index.html`
**MONITORING ONLY**

Watch the workflow in action:
- Task status (New → Working → Done)
- Real-time agent activity logs
- Filter by status
- Download reports

### 3. 🏰 Hierarchy Visualization - `/hierarchy.html`
**MONITORING ONLY**

Beautiful animated view:
- Live statistics dashboard
- Interactive hierarchy tree
- Project details
- Service health status
- Auto-refreshes every 5 seconds

## 💡 Usage Examples

### Example 1: Todo API
**You:** "I need a REST API for a todo list with CRUD operations"
**Result:** Working API in `./workspace/todo-api/`

### Example 2: Blog Platform
**You:** "Build a blog with authentication, posts, comments, and React frontend"
**Result:** Full application in `./workspace/blog-platform/`

### Example 3: Payment Service
**You:** "Create a Stripe payment microservice with webhooks and email receipts"
**Result:** Production-ready service in `./workspace/payment-service/`

## 📁 Directory Structure

```
.
├── specifications/           # Dragon creates specs here
│   └── your-project.md
│
├── projects/                 # Project metadata (auto-managed)
│   └── projects.json
│
└── workspace/                # Generated code appears here
    └── your-project/
        ├── *-analysis.md     # Wyrm analysis
        ├── *-tasks.md        # Task lists
        ├── src/              # Source code
        ├── tests/            # Tests
        └── docs/             # Documentation
```

## ⚙️ Configuration

### Supported Providers

| Provider | Env Variable | Models |
|----------|-------------|--------|
| OpenAI | `OPENAI_API_KEY` | gpt-4o, gpt-4 |
| Claude | `ANTHROPIC_API_KEY` | claude-3-5-sonnet |
| Gemini | `GEMINI_API_KEY` | gemini-2.0-flash |
| Azure OpenAI | `AZURE_OPENAI_ENDPOINT` + `AZURE_OPENAI_API_KEY` | Custom |
| Ollama | None (local) | llama3.2, mistral |

### Advanced Settings

`appsettings.local.json`:
```json
{
  "Orchestrator": {
    "Provider": "openai",
    "ApiKey": "your-key",
    "Model": "gpt-4o"
  }
}
```

## 🎯 Best Practices

### ✅ DO:
- Be specific with Dragon ("Create a REST API with JWT auth and PostgreSQL")
- Let the workflow complete (wait for background services)
- Monitor progress on Status/Hierarchy pages
- Review generated code before deploying

### ❌ DON'T:
- Be vague ("Make an app")
- Interrupt the automatic process
- Expect instant results (services run every 60s)
- Commit API keys to version control

## 🛠️ Troubleshooting

**Dragon not responding?**
- Check API key configuration
- Verify LLM provider is accessible
- Check browser console for errors

**Wyrm not processing?**
- Wait 60 seconds for next check cycle
- Verify spec saved to `./specifications/`
- Check Status Monitor logs

**No code generated?**
- Ensure Wyrm created tasks
- Verify Drake assigned tasks to Kobolds
- Check Status Monitor for errors

## 🔒 Security

- ✅ Sandboxed file operations
- ✅ Local API key storage
- ✅ Code stays on your machine
- ⚠️ Review code before production
- ⚠️ Never commit API keys

## 📚 Learn More

- [Main README](../README.md) - Complete DraCode documentation
- [Architecture](../docs/architecture/) - System design
- [Provider Setup](../docs/setup-guides/) - Configure your LLM

## 🤝 Contributing

Contributions welcome! See [main README](../README.md) for guidelines.

## 📝 License

MIT License - See [LICENSE](../LICENSE)

---

**Built with ❤️ using .NET 10.0 and AI agents**

*KoboldTown - Where AI agents collaborate to build your projects automatically*
