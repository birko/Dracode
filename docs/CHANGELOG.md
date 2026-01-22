# DraCode Changelog

All notable changes to this project will be documented in this file.

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
| 2.1 | Jan 2026 | Multi-task execution, batch processing |
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
