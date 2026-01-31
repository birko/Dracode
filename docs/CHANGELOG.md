# DraCode Changelog

All notable changes to this project will be documented in this file.

---

## [2.2.0] - 2026-01-31

### 🚀 Added - New LLM Providers

**Expanded from 7 to 10+ LLM providers with OpenAI-compatible base:**

#### New Providers
- **ZAiProvider** (`zai`) - Z.AI (formerly Zhipu AI) GLM models
  - GLM-4.5-flash, GLM-4.6-flash, GLM-4.7 models
  - International and China endpoints
  - Deep Thinking mode support
  - Environment: `ZHIPU_API_KEY`
- **VllmProvider** (`vllm`) - vLLM local inference server
  - High-performance local LLM serving
  - OpenAI-compatible API
- **SglangProvider** (`sglang`) - SGLang inference server
  - Structured generation support
  - OpenAI-compatible API
- **OpenAiCompatibleProviderBase** - Base class for OpenAI-compatible APIs
  - Shared implementation for vLLM, SGLang, LlamaCpp
  - Easy extension for other OpenAI-compatible servers

### 🐉 Added - Dragon Enhancement Tools

**New tools for Dragon interactive agent:**

- **AddExistingProjectTool** (`add_existing_project`)
  - Scan existing directories and import as projects
  - Auto-detect technologies (50+ file extensions mapped)
  - Analyze project structure and dependencies
  - Generate initial specifications from existing code
- **ProjectApprovalTool** (`approve_specification`)
  - Two-stage specification workflow: Prototype → Approved
  - User must explicitly approve before Wyvern processes
  - Prevents accidental task generation from incomplete specs

### 🔧 Changed - Architecture Improvements

- **Model Reorganization**: Models split into logical subdirectories
  - `Models/Agents/` - DragonMessage, DrakeStatistics, Kobold, etc.
  - `Models/Configuration/` - ProjectConfig, ProviderConfig, UserSettings
  - `Models/Projects/` - Project, ProjectInfo, Specification, WorkArea
  - `Models/Tasks/` - Feature, TaskRecord, TaskStatus, TaskTracker
  - `Models/WebSocket/` - WebSocketCommand, WebSocketRequest
- **WyrmFactory**: New factory for creating Wyrm analyzers
- **Configuration Overhaul**: Simplified appsettings.json, local config example

### 🎨 Changed - UI Improvements

- **Compacted UI**: Streamlined interface with better space utilization
- **Project Config View**: New dedicated view for project configuration
- **Dragon View**: Shows only non-empty messages on reload

### 🐛 Fixed

- Provider configuration loading and validation
- Serialization issues in WebSocket communication
- Client ID handling in multi-session scenarios

---

## [Unreleased]

### 🎨 Added - New Specialized Agent Types

**Added 6 new specialized agents - expanding from 11 to 17 total agent types:**

#### New Coding Agents
- **PhpCodingAgent** (`php`) - PHP specialist for Laravel, Symfony, WordPress
  - Modern PHP 8.0+ with type declarations, PSR standards
  - Composer, PHPUnit, security best practices
- **PythonCodingAgent** (`python`) - Python specialist for web and data science
  - Django, Flask, FastAPI frameworks
  - NumPy, Pandas, TensorFlow, PyTorch
  - PEP 8 compliance, type hints, async/await

#### New Media Agent Hierarchy
- **MediaAgent** (`media`) - General digital media specialist
  - Images, video, audio formats and optimization
- **ImageAgent** (`image`) - Image specialist (derives from MediaAgent)
  - Raster and vector image handling
  - Responsive images, accessibility
- **SvgAgent** (`svg`) - SVG graphics specialist (derives from ImageAgent)
  - Scalable vector graphics, D3.js, animations
  - Optimization, accessibility features
- **BitmapAgent** (`bitmap`) - Bitmap/raster specialist (derives from ImageAgent)
  - JPEG, PNG, WebP optimization
  - Retina displays, compression techniques

### 🔧 Changed - KoboldLair Integration
- WyvernAgent updated to recognize all 17 agent types
- WyvernAgent system prompt expanded with new agent descriptions
- Drake can now create Kobolds with new specialized agents
- Automatic task assignment based on agent specialization
- KoboldFactory documentation updated

### 📚 Documentation Updates
- **NEW**: `docs/NEW_AGENT_TYPES.md` - Comprehensive guide for new agents
- Updated `README.md` - Now lists 17 specialized agent types
- Updated `DraCode.KoboldLair.Server/README.md` - Agent specializations section
- Updated `DraCode.KoboldLair.Client/README.md` - Web UI documentation
- Updated `docs/README.md` - Added reference to new agent types
- Updated documentation across all components

### 💡 Usage Examples
```csharp
// PHP Laravel development
var phpAgent = AgentFactory.Create("openai", options, config, "php");
await phpAgent.RunAsync("Create Laravel authentication system");

// Python data science
var pythonAgent = AgentFactory.Create("claude", options, config, "python");
await pythonAgent.RunAsync("Analyze CSV data with pandas");

// SVG graphics
var svgAgent = AgentFactory.Create("openai", options, config, "svg");
await svgAgent.RunAsync("Create animated SVG logo");

// Image optimization
var bitmapAgent = AgentFactory.Create("gemini", options, config, "bitmap");
await bitmapAgent.RunAsync("Optimize photos for web");
```

---

## [2.0.5] - January 2026 - WebSocket Authentication with IP Binding

### 🔐 New Feature: Token-Based Authentication with IP Address Binding

**WebSocket server now supports optional token-based authentication with IP address binding to prevent token misuse!**

#### Features
- ✅ Token-based authentication for WebSocket connections
- ✅ IP address binding prevents stolen tokens from being used
- ✅ Supports both simple tokens and IP-bound tokens
- ✅ Environment variable support for tokens and IPs
- ✅ Automatic client IP detection (handles proxies via X-Forwarded-For, X-Real-IP)
- ✅ Backward compatible - disabled by default
- ✅ Flexible configuration - mix simple and IP-bound tokens

#### Configuration

**Simple token authentication:**
```json
{
  "Authentication": {
    "Enabled": true,
    "Tokens": ["${WEBSOCKET_AUTH_TOKEN}"]
  }
}
```

**Token with IP binding (recommended for production):**
```json
{
  "Authentication": {
    "Enabled": true,
    "TokenBindings": [
      {
        "Token": "${WEBSOCKET_RESTRICTED_TOKEN}",
        "AllowedIps": ["192.168.1.100", "10.0.0.50"]
      }
    ]
  }
}
```

#### Connection

**Without authentication:**
```
ws://localhost:5000/ws
```

**With authentication:**
```
ws://localhost:5000/ws?token=your-secret-token
```

#### Security Benefits
- **Token theft prevention**: IP-bound tokens can't be used from unauthorized IPs
- **Audit logging**: Failed authentication attempts logged with IP addresses
- **Proxy support**: Works behind reverse proxies and load balancers
- **Environment variables**: Store tokens securely outside of code

#### Files Created
- `DraCode.WebSocket/Models/AuthenticationConfiguration.cs` - Config model with TokenIpBinding
- `DraCode.WebSocket/Services/WebSocketAuthenticationService.cs` - Validation logic

#### Files Modified
- `DraCode.WebSocket/Program.cs` - Added authentication checks before accepting connections
- `DraCode.WebSocket/appsettings.json` - Added Authentication section
- `DraCode.WebSocket/appsettings.local.json.example` - Added example configurations
- `DraCode.WebSocket/README.md` - Comprehensive authentication documentation
- `README.md` - Updated security notes
- `docs/setup-guides/WEBSOCKET_QUICKSTART.md` - Added authentication section
- `DraCode.AppHost/README.md` - Added authentication note
- `DraCode.Web/README.md` - Added authentication note

#### Documentation
See [DraCode.WebSocket/README.md](../DraCode.WebSocket/README.md#authentication) for complete authentication documentation.

---

## [2.0.4] - January 2026 - Multiple Connections to Same Provider

### ✨ New Feature: Multiple Provider Instances

**You can now connect to the same provider multiple times with independent agent instances!**

#### Features
- ✅ Unlimited connections per provider
- ✅ Auto-numbered tabs (OpenAI, OpenAI #2, OpenAI #3, etc.)
- ✅ Connection count display on provider cards
- ✅ Real-time updates as connections change
- ✅ Independent conversation history per instance

#### Use Cases
- **Response Comparison**: Compare outputs for different prompts
- **Parallel Tasks**: Run multiple tasks simultaneously
- **Context Isolation**: Test that instances don't share context
- **A/B Testing**: Test different approaches side-by-side

#### Visual Design
- **Connection Count**: "🔗 2 active connections"
- **Smart Naming**: Auto-numbered tabs for multiple instances
- **Real-time Updates**: Grid refreshes when connections change

#### Files Modified
- `DraCode.Web/src/client.ts` - Removed connection limit, added smart naming
- `DraCode.Web/wwwroot/styles.css` - Added connection count styles

See [MULTIPLE_CONNECTIONS_FEATURE.md](MULTIPLE_CONNECTIONS_FEATURE.md) for complete documentation.

---

## [2.0.3] - January 2026 - Clickable Links in Activity Log

### ✨ New Feature: Clickable Links

**Activity logs now automatically detect and linkify URLs!**

#### Features
- ✅ Automatic URL detection (http, https, ws, wss, ftp)
- ✅ Clickable links that open in new tab
- ✅ Visual hover effects and visited link tracking
- ✅ Security: `rel="noopener noreferrer"` protection

#### Supported URL Types
- HTTP/HTTPS: `https://example.com/api`
- WebSocket: `ws://localhost:5000/ws`
- Secure WebSocket: `wss://secure.example.com`
- FTP: `ftp://files.example.com`

#### Visual Design
- **Link Color**: Light blue (#4fc3f7)
- **Hover**: Brighter blue (#29b6f6)
- **Visited**: Purple (#ba68c8)
- **Underline**: Shows by default, removes on hover

#### Files Modified
- `DraCode.Web/src/client.ts` - Added `linkifyUrls()` method
- `DraCode.Web/wwwroot/styles.css` - Added link styles
- Test page: `http://localhost:5001/link-test.html`

See [CLICKABLE_LINKS_FEATURE.md](CLICKABLE_LINKS_FEATURE.md) for complete documentation.

---

## [2.0] - January 2026 - WebSocket Multi-Agent System

### 🎯 Major Feature: Multi-Agent WebSocket System

A complete multi-agent WebSocket system that allows users to connect to multiple LLM providers simultaneously through a single WebSocket connection.

#### New Projects Created

**1. DraCode.WebSocket** (WebSocket API Server)
- Single WebSocket connection supports multiple agents
- Each agent identified by unique `agentId`
- Server-side LLM configuration with environment variable expansion
- Commands: list, connect, disconnect, reset, send

**2. DraCode.Web** (Web Client)
- Multi-provider interface with provider grid
- Tabbed interface for multiple active agents
- Separate activity logs per agent
- Real-time agent responses

**3. DraCode.AppHost** (.NET Aspire Orchestration)
- Single-command startup for entire system
- Service discovery and health checks
- Aspire Dashboard for monitoring

#### Key Features
- ✅ Connect to multiple providers simultaneously
- ✅ Side-by-side response comparison
- ✅ Independent conversation history per agent
- ✅ Tabbed provider interface
- ✅ Secure server-side configuration
- ✅ Zero-dependency modern TypeScript client

---

## [2.0.1] - January 2026 - Web Client Modernization

### 🔄 Complete TypeScript & CSS Modernization

**Removed Bootstrap**
- Deleted `/wwwroot/lib/bootstrap/` directory
- Removed all Bootstrap CSS classes and JavaScript

**TypeScript Implementation**
- Fully typed TypeScript codebase with ES modules
- Type definitions in `src/types.ts`
- Modular architecture: types, client, ui, main
- Zero runtime dependencies (vanilla TypeScript only)

**Modern CSS**
- Custom Flexbox-based layout system
- CSS Grid for provider cards
- Modern CSS variables for theming
- Responsive design without frameworks

**Tech Stack**
- TypeScript 5.7.3 (ES2022 modules)
- Vanilla CSS3 (Flexbox + Grid)
- Native WebSocket API
- Zero dependencies!

---

## [2.0.2] - January 2026 - Bug Fixes

### 🐛 Fixed: Property Name Case Mismatch

**Issue**: Provider list not displaying in web client due to case sensitivity.

**Root Cause**: C# server uses PascalCase (`Status`, `Message`, `Data`) but TypeScript client was using camelCase (`status`, `message`, `data`).

**Solution**: Updated TypeScript interfaces to match C# PascalCase properties.

**Files Modified**:
- `DraCode.Web/src/types.ts` - Updated interface definitions
- `DraCode.Web/src/client.ts` - Updated all property references
- `DraCode.Web/wwwroot/inspector.html` - Updated diagnostic checks

**Impact**: Provider grid now displays correctly after connection.

---

### 🔧 Enhancement: Message Handler Debugging

**Added debugging tools for WebSocket message handling**:

1. **Enhanced Logging** - Detailed message handling steps in console
2. **Inspector Tool** - `http://localhost:5001/inspector.html` for message analysis
3. **Debug Console Commands** - `debugShowProviders()`, `debugCheckElements()`

**Files Created**:
- `wwwroot/inspector.html` - Message inspection tool
- Enhanced logging in `src/client.ts`
- Debug helpers in `src/main.ts`

---

## [2.1] - January 2026 - Multi-Task Execution

### 🎯 Major Feature: Multi-Task Sequential Execution

Execute multiple tasks sequentially with fresh agent instances for each task.

#### Core Capabilities
- **Sequential Execution**: Tasks run one after another
- **Context Isolation**: Each task gets fresh agent instance
- **Progress Tracking**: Visual indicators (Task N/Total)
- **Error Handling**: Failures don't stop subsequent tasks
- **Flexible Input**: 3 ways to define tasks
- **Batch Processing**: Ideal for CI/CD workflows

#### Input Methods

| Method | Format | Example |
|--------|--------|---------|
| Command-line | Comma-separated | `--task="T1,T2,T3"` |
| Interactive | Multi-line prompt | Task 1: ... ↵ Task 2: ... ↵ ↵ |
| Configuration | JSON array | `"Tasks": ["T1", "T2"]` |

#### Configuration Changes

**Before:**
```json
{
  "Agent": {
    "TaskPrompt": "Single task here"
  }
}
```

**After:**
```json
{
  "Agent": {
    "Tasks": [
      "First task",
      "Second task",
      "Third task"
    ]
  }
}
```

#### UI Output
```
── Starting Execution of 3 Tasks ──

── Task 1/3 ──
📝 Create project structure
✓ Task 1 completed successfully

── Task 2/3 ──
📝 Implement core functionality
✓ Task 2 completed successfully

── Task 3/3 ──
📝 Add unit tests
✓ Task 3 completed successfully

── All Tasks Complete (3/3) ──
```

#### Documentation Updated
- Architecture Specification - Added multi-task execution flow
- Technical Specification - Added multi-task system section
- Implementation Plan - Added Phase 6 (Multi-Task Execution)
- README - Added multi-task usage examples
- CLI Options - Added multi-task syntax documentation

---

## [1.0] - Initial Release

### Features
- Multi-provider LLM support (6 providers)
- Tool system with 7 tools
- Interactive provider selection menu
- Spectre.Console UI integration
- GitHub Copilot OAuth integration
- Path sandboxing and security
- Configuration system with env var support
- Command-line arguments parsing
- Conversational loop with iteration limits

### Supported Providers
- OpenAI (gpt-4o, gpt-4, gpt-3.5-turbo)
- Claude (claude-3-5-sonnet, claude-3-5-haiku, claude-3-opus)
- Gemini (gemini-2.0-flash-exp)
- Azure OpenAI (custom deployments)
- Ollama (local models: llama3.2, mistral, codellama)
- GitHub Copilot (gpt-4o, gpt-4-turbo via OAuth)

### Tools System
- `list_files` - Directory listing with recursive search
- `read_file` - Read file contents
- `write_file` - Create/modify files
- `search_code` - Grep-like code search with regex
- `run_command` - Execute shell commands with timeout
- `ask_user` - Interactive user prompts
- `display_text` - Formatted text output

---

## Version History

| Version | Date | Key Features |
|---------|------|--------------|
| 2.2.0 | Jan 2026 | Z.AI/vLLM/SGLang providers, Dragon tools, model reorganization |
| 2.1 | Jan 2026 | Multi-task execution, batch processing |
| 2.0.5 | Jan 2026 | WebSocket authentication with IP binding |
| 2.0.4 | Jan 2026 | Multiple connections to same provider |
| 2.0.3 | Jan 2026 | Clickable links in activity log |
| 2.0.2 | Jan 2026 | Bug fixes (case sensitivity, message handler) |
| 2.0.1 | Jan 2026 | Web client modernization (TypeScript, CSS) |
| 2.0 | Jan 2026 | WebSocket multi-agent system |
| 1.0 | Dec 2025 | Initial release with 6 providers |

---

## Future Roadmap

### Planned Features
- [ ] Parallel task execution
- [ ] Task dependencies
- [ ] Conditional execution
- [ ] Task templates and reusable workflows
- [ ] Persistent task queue
- [ ] Unit test coverage
- [ ] Integration tests with mock LLMs
- [ ] Performance optimizations

### Under Consideration
- [ ] Python SDK for agent integration
- [ ] VS Code extension
- [ ] Docker containerization
- [ ] Cloud deployment templates
- [ ] Agent marketplace

---

## Migration Notes

### v2.1 Migration (Multi-Task)
- Change `TaskPrompt` to `Tasks` array in configuration
- Backward compatible: single task via CLI still works
- Empty `Tasks: []` triggers interactive prompt

### v2.0 Migration (WebSocket)
- New projects: DraCode.WebSocket, DraCode.Web, DraCode.AppHost
- Use .NET Aspire for orchestration: `dotnet run --project DraCode.AppHost`
- Server-side configuration required for providers

---

**Maintained By**: DraCode Team  
**License**: MIT
